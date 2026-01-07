using JiebaNet.Segmenter;
using System;
using System.Collections.Generic;

namespace JiebaNet
{
    internal interface ICutMethodsApi
    {
        IEnumerable<string> Cut(ReadOnlySpan<char> sentence);
    }

    class All_CutMethodsApi : ICutMethodsApi
    {
        private readonly JiebaSegmenter segmenter;

        public All_CutMethodsApi(JiebaSegmenter segmenter)
        {
            this.segmenter = segmenter;
        }

        public IEnumerable<string> Cut(ReadOnlySpan<char> sentence)
        {
            // 按索引顺序访问 DAG，避免依赖字典的枚举顺序
            var dag = segmenter.GetDag(sentence);

            var words = new List<string>();
            var lastPos = -1;
            var n = sentence.Length;

            for (int k = 0; k < n; k++)
            {
                if (!dag.TryGetValue(k, out var nexts))
                {
                    // 若没有 entry，退化为单字符
                    if (k > lastPos)
                    {
                        var s = sentence.Slice(k, 1).ToString();
                        words.Add(s);
                        lastPos = k;
                    }
                    continue;
                }

                if (nexts.Count == 1 && k > lastPos)
                {
                    var end = nexts[0];
                    words.Add(sentence.Slice(k, end + 1 - k).ToString());
                    lastPos = end;
                }
                else
                {
                    foreach (var j in nexts)
                    {
                        if (j > k)
                        {
                            words.Add(sentence.Slice(k, j + 1 - k).ToString());
                            lastPos = j;
                        }
                    }
                }
            }

            return words;
        }

        [Obsolete]
        public IEnumerable<string> Cut_Obsolete(ReadOnlySpan<char> sentence)
        {
            var dag = segmenter.GetDag(sentence);

            var words = new List<string>();
            var lastPos = -1;

            foreach (var pair in dag)
            {
                var k = pair.Key;
                var nexts = pair.Value;
                if (nexts.Count == 1 && k > lastPos)
                {
                    words.Add(sentence.Slice(k, nexts[0] + 1 - k).ToString());
                    lastPos = nexts[0];
                }
                else
                {
                    foreach (var j in nexts)
                    {
                        if (j > k)
                        {
                            words.Add(sentence.Slice(k, j + 1 - k).ToString());
                            lastPos = j;
                        }
                    }
                }
            }

            return words;
        }
    }

    class Dag_CutMethodsApi : ICutMethodsApi
    {

        private readonly JiebaSegmenter segmenter;

        public Dag_CutMethodsApi(JiebaSegmenter segmenter)
        {
            this.segmenter = segmenter;
        }

        public IEnumerable<string> Cut(ReadOnlySpan<char> sentence)
        {
            var dag = segmenter.GetDag(sentence);
            var route = segmenter.Calc(sentence, dag);

            var tokens = new List<string>();

            var x = 0;
            var n = sentence.Length;
            var buf = StringBuilderPool.Instance.Get();
            try
            {
                while (x < n)
                {
                    var y = route[x].Key + 1;
                    if (y == x) throw new Exception("Calc Method Return Exception Result.");
                    var w = sentence.Slice(x, y - x);
                    if (y - x == 1)
                    {
                        buf.Append(w);
                    }
                    else
                    {
                        if (buf.Length > 0)
                        {
                            segmenter.AddBufferToWordList(tokens, buf.ToString());
                            buf.Clear();
                        }
                        tokens.Add(w.ToString());
                    }
                    x = y;
                }

                if (buf.Length > 0)
                {
                    segmenter.AddBufferToWordList(tokens, buf.ToString());
                }
            }
            finally
            {
                StringBuilderPool.Instance.Return(buf);
            }
            return tokens;
        }


    }

    class DagWithoutHmm_CutMethodsApi : ICutMethodsApi
    {
        private readonly JiebaSegmenter segmenter;

        public DagWithoutHmm_CutMethodsApi(JiebaSegmenter segmenter)
        {
            this.segmenter = segmenter;
        }

        private bool is_english(char c) => ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'));
        public IEnumerable<string> Cut(ReadOnlySpan<char> sentence)
        {
            var dag = segmenter.GetDag(sentence);
            var route = segmenter.Calc(sentence, dag);

            var words = new List<string>();

            var x = 0;
            var buf = StringBuilderPool.Instance.Get();
            try
            {
                var n = sentence.Length;
                while (x < n)
                {
                    var y = route[x].Key + 1;
                    var l_word = sentence.Slice(x, y - x);
                    if (l_word.Length == 1 && is_english(l_word[0]))
                    {
                        buf.Append(l_word);
                        x = y;
                    }
                    else
                    {
                        if (buf.Length > 0)
                        {
                            words.Add(buf.ToString());
                            buf.Clear();
                        }
                        words.Add(l_word.ToString());
                        x = y;
                    }
                }

                if (buf.Length > 0)
                {
                    words.Add(buf.ToString());
                }
            }
            finally
            {
                StringBuilderPool.Instance.Return(buf);
            }
            return words;
        }
    }
}
