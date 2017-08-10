using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using opennlp.tools.chunker;
using opennlp.tools.postag;
using opennlp.tools.sentdetect;
using opennlp.tools.tokenize;

namespace OpenNLP.NET.PoC
{
    public class AbstractNounPhraseAdapter
    {
        protected readonly string ModelsPath;

        /// <summary>
        /// A path to the directory where the OpenNLP models are located.
        /// </summary>
        protected AbstractNounPhraseAdapter(string modelsPath)
        {
            ModelsPath = modelsPath;
        }

        /// <summary>
        /// Return the OpenNLP analyzer given its model type (M), the type of the analyzer (T), the filename
        /// of the model (i.e. en-maxent.bin) and a path to where the Models are lcoated (ModelsPath).
        /// </summary>
        public T ResolveOpenNlpTool<M, T>(string modelPath)
            where T : class
            where M : class
        {
            var modelStream = new java.io.FileInputStream(Path.Combine(ModelsPath, modelPath));

            M model;
            try
            {
                model = (M)Activator.CreateInstance(typeof(M), modelStream);
            }
            finally
            {
                if (modelStream != null)
                {
                    modelStream.close();
                }
            }

            return (T)Activator.CreateInstance(typeof(T), model);
        }

        /// <summary>
        /// Functions to run after PoS parsing to determine if the noun phrase should be returned.
        /// </summary>
        public IEnumerable<Func<string, bool>> PostProcessingFilters { get; set; }

        protected bool ValidNounPhrase(string nounPhrase)
        {
            return PostProcessingFilters == null ||
                    PostProcessingFilters.Aggregate(true, (current, filter) => current && filter.Invoke(nounPhrase));
        }
    }
}

namespace OpenNLP.NET.PoC
{
    /// <summary>
    /// Ported from Java implementation by Sujit Pal
    /// http://sujitpal.blogspot.ca/2011/08/uima-noun-phrase-pos-annotator-using.html
    /// </summary>
    public class PosNounPhraseParser : AbstractNounPhraseAdapter
    {
        public PosNounPhraseParser(string modelsPath) : base(modelsPath) { }

        private static SentenceDetector _sentenceDetector;
        private SentenceDetector GetSentenceDetector()
        {
            return _sentenceDetector ?? (_sentenceDetector = ResolveOpenNlpTool<SentenceModel, SentenceDetectorME>("en-sent.bin"));
        }

        private static POSTagger _posTagger;
        private POSTagger GetPosTagger()
        {
            return _posTagger ?? (_posTagger = ResolveOpenNlpTool<POSModel, POSTaggerME>("en-pos-maxent.bin"));
        }

        private static Tokenizer _tokenizer;
        private Tokenizer GetTokenizer()
        {
            return _tokenizer ?? (_tokenizer = ResolveOpenNlpTool<TokenizerModel, TokenizerME>("en-token.bin"));
        }

        private static Chunker _chunker;
        private Chunker GetChunker()
        {
            return _chunker ?? (_chunker = ResolveOpenNlpTool<ChunkerModel, ChunkerME>("en-chunker.bin"));
        }

        public void WarmUpModels()
        {
            GetSentenceDetector();
            GetPosTagger();
            GetTokenizer();
            GetChunker();
        }

        public IList<string> GetNounPhrases(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText)) throw new ArgumentNullException("sourceText");

            var nounPhrases = new List<string>();

            // return an array of start and end indexes that identify sentences
            var sentenceSpans = GetSentenceDetector().sentPosDetect(sourceText);
            foreach (var sentenceSpan in sentenceSpans)
            {
                // retrieve the actual sentence from the source text
                var sentence = sentenceSpan.getCoveredText(sourceText).toString();
                var start = sentenceSpan.getStart();

                // return an array of start and end indexes that identify various
                // tokens/tags in the sentence (i.e. noun phrases, verb phrases, etc)
                var tokenSpans = GetTokenizer().tokenizePos(sentence);
                var tokens = new string[tokenSpans.Length];
                for (var i = 0; i < tokens.Length; i++)
                {
                    tokens[i] = tokenSpans[i].getCoveredText(sentence).toString();
                }
                var tags = GetPosTagger().tag(tokens);

                // return an array of chunks that contain tag types and start/end indexes
                // for the chunk in the source text
                var chunks = GetChunker().chunkAsSpans(tokens, tags);

                foreach (var chunk in chunks)
                {
                    // filter out everything but noun phrases
                    if (chunk.getType() != "NP") continue;

                    var chunkStart = start + tokenSpans[chunk.getStart()].getStart();
                    var chunkEnd = start + tokenSpans[chunk.getEnd() - 1].getEnd();

                    // extract the noun phrase
                    var nounPhrase = sourceText.Substring(chunkStart, chunkEnd - chunkStart);

                    // run post processing functions to determine if this noun phrase
                    // is suitable for our purposes (defined by caller)
                    if (!ValidNounPhrase(nounPhrase)) continue;

                    nounPhrases.Add(nounPhrase);
                }
            }
            return nounPhrases;
        }
    }
}