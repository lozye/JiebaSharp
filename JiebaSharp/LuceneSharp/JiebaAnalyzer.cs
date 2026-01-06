#if LuceneSharp
using JiebaNet.Segmenter;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using System.Collections.Generic;
using System.IO;

namespace JiebaNet.LuceneSharp
{
    public class JiebaAnalyzer : Analyzer
    {
        private readonly JiebaSegmenter _segmenter;
        private readonly bool _useHmm;
        private readonly TokenizerMode _tokenizerMode;

        public JiebaAnalyzer(bool useHmm = true, TokenizerMode tokenizerMode = TokenizerMode.Default)
        {
            _segmenter = new JiebaSegmenter();
            _useHmm = useHmm;
            _tokenizerMode = tokenizerMode;
        }

        protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var tokenizer = new JiebaTokenizer(reader, _segmenter, _useHmm, _tokenizerMode);
            return new TokenStreamComponents(tokenizer);
        }

        public void AddWords(IList<string> words)
        {
            foreach (var word in words)
                _segmenter.AddWord(word);
        }
    }

    public sealed class JiebaTokenizer : Tokenizer
    {
        private readonly JiebaSegmenter _segmenter;
        private readonly bool _useHmm;
        private readonly TokenizerMode _tokenizerMode;
        private readonly ICharTermAttribute _termAtt;
        private readonly IOffsetAttribute _offsetAtt;

        private List<Segmenter.Token> _tokens;
        private int _position = 0;
        private string _inputText;

        public JiebaTokenizer(TextReader input, JiebaSegmenter segmenter, bool useHmm, TokenizerMode tokenizerMode) : base(input)
        {
            _segmenter = segmenter;
            _useHmm = useHmm;
            _termAtt = AddAttribute<ICharTermAttribute>();
            _offsetAtt = AddAttribute<IOffsetAttribute>();
            _tokenizerMode = tokenizerMode;
        }

        public override bool IncrementToken()
        {
            // 第一次读取时进行分词
            if (_tokens == null)
            {
                _inputText = base.m_input.ReadToEnd();
                // Tokenize 方法返回带 Offset 的 Token
                var jiebaTokens = _segmenter.Tokenize(_inputText, _tokenizerMode, _useHmm);
                _tokens = new List<Segmenter.Token>(jiebaTokens);
                _position = 0;
            }

            if (_position < _tokens.Count)
            {
                var token = _tokens[_position];

                ClearAttributes();
                _termAtt.SetEmpty().Append(token.Word);
                _offsetAtt.SetOffset(token.StartIndex, token.EndIndex);

                _position++;
                return true;
            }

            return false;
        }

        public override void Reset()
        {
            base.Reset();
            _tokens = null;
            _position = 0;
        }
    }
}
#endif