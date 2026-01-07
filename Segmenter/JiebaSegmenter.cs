using JiebaNet.Segmenter.Common;
using JiebaNet.Segmenter.FinalSeg;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace JiebaNet.Segmenter
{
    public class JiebaSegmenter
    {
        private readonly WordDictionary WordDict;
        private static readonly IFinalSeg FinalSeg = Viterbi.Instance;
        private static readonly ISet<string> LoadedPath = new HashSet<string>();

        private static readonly object locker = new object();

        internal IDictionary<string, string> UserWordTagTab { get; set; }
        private readonly ICutMethodsApi[] CutMethods;

        #region Regular Expressions

        internal static readonly Regex RegexChineseDefault = new Regex(@"([\u4E00-\u9FD5a-zA-Z0-9+#&\._]+)", RegexOptions.Compiled);

        internal static readonly Regex RegexSkipDefault = new Regex(@"(\r\n|\s)", RegexOptions.Compiled);

        internal static readonly Regex RegexChineseCutAll = new Regex(@"([\u4E00-\u9FD5]+)", RegexOptions.Compiled);
        internal static readonly Regex RegexSkipCutAll = new Regex(@"[^a-zA-Z0-9+#\n]", RegexOptions.Compiled);

        internal static readonly Regex RegexEnglishChars = new Regex(@"[a-zA-Z0-9]", RegexOptions.Compiled);

        internal static readonly Regex RegexUserDict = new Regex("^(?<word>.+?)(?<freq> [0-9]+)?(?<tag> [a-z]+)?$", RegexOptions.Compiled);

        #endregion

        public JiebaSegmenter()
        {
            WordDict = WordDictionary.Instance.New();
            UserWordTagTab = new Dictionary<string, string>(StringComparer.Ordinal);
            CutMethods = new ICutMethodsApi[] { new All_CutMethodsApi(this), new Dag_CutMethodsApi(this), new DagWithoutHmm_CutMethodsApi(this) };
        }

        /// <summary>
        /// The main function that segments an entire sentence that contains 
        /// Chinese characters into seperated words.
        /// </summary>
        /// <param name="text">The string to be segmented.</param>
        /// <param name="cutAll">Specify segmentation pattern. True for full pattern, False for accurate pattern.</param>
        /// <param name="hmm">Whether to use the Hidden Markov Model.</param>
        /// <returns></returns>
        public IEnumerable<string> Cut(string text, bool cutAll = false, bool hmm = true)
        {
            var words = CutWords(text, cutAll, hmm);
            return words.Select(x => x.value);
        }

        public IEnumerable<WordInfo> CutWords(string text, bool cutAll = false, bool hmm = true)
        {
            var reHan = RegexChineseDefault;
            var reSkip = RegexSkipDefault;

            if (cutAll)
            {
                reHan = RegexChineseCutAll;
                reSkip = RegexSkipCutAll;
            }

            var method_api = cutAll ? CutMethods[0] : (hmm ? CutMethods[1] : CutMethods[2]);
            return CutWordsCore(text, method_api, reHan, reSkip, cutAll);
        }

        public IEnumerable<string> CutForSearch(string text, bool hmm = true)
        {
            var result = new List<string>();

            var words = CutWords(text, hmm: hmm);
            foreach (var word in words)
            {
                var w = word.value;
                if (w.Length > 2)
                {
                    for (var i = 0; i < w.Length - 1; i++)
                    {
                        var gram2 = w.Substring(i, 2);
                        if (WordDict.ContainsWord(gram2))
                        {
                            result.Add(gram2);
                        }
                    }
                }

                if (w.Length > 3)
                {
                    for (var i = 0; i < w.Length - 2; i++)
                    {
                        var gram3 = w.Substring(i, 3);
                        if (WordDict.ContainsWord(gram3))
                        {
                            result.Add(gram3);
                        }
                    }
                }

                result.Add(w);
            }

            return result;
        }

        public IEnumerable<Token> Tokenize(string text, TokenizerMode mode = TokenizerMode.Default, bool hmm = true)
        {
            var result = new List<Token>();

            if (mode == TokenizerMode.Default)
            {
                foreach (var w in CutWords(text, hmm: hmm))
                {
                    var width = w.value.Length;
                    result.Add(new Token(w.value, w.position, w.position + width));
                }
            }
            else
            {
                //var xx = Cut2(text, hmm: hmm);
                foreach (var w in CutWords(text, hmm: hmm))
                {
                    var width = w.value.Length;
                    if (width > 2)
                    {
                        for (var i = 0; i < width - 1; i++)
                        {
                            var gram2 = w.value.Substring(i, 2);
                            if (WordDict.ContainsWord(gram2))
                            {
                                result.Add(new Token(gram2, w.position + i, w.position + i + 2));
                            }
                        }
                    }
                    if (width > 3)
                    {
                        for (var i = 0; i < width - 2; i++)
                        {
                            var gram3 = w.value.Substring(i, 3);
                            if (WordDict.ContainsWord(gram3))
                            {
                                result.Add(new Token(gram3, w.position + i, w.position + i + 3));
                            }
                        }
                    }

                    result.Add(new Token(w.value, w.position, w.position + width));

                }
            }

            return result;
        }

        #region Internal Cut Methods


        /// <summary>
        /// 使用 ReadOnlySpan 优化的 DAG 生成
        /// </summary>
        public Dictionary<int, List<int>> GetDag(ReadOnlySpan<char> sentence)
        {
            var dag = new Dictionary<int, List<int>>();
            int n = sentence.Length;

            for (int k = 0; k < n; k++)
            {
                var tmpList = new List<int>();
                int i = k;

                // 初始切片：长度为1
                ReadOnlySpan<char> frag = sentence.Slice(k, 1);

                // 循环条件：索引不越界 且 词典包含该前缀
                // lookup.ContainsKey(frag) 不会产生任何内存分配
                while (i < n && WordDict.TryGetValue(frag, out var seq))
                {
                    // 获取词频 (Python: if self.FREQ[frag])
                    if (seq > 0)
                    {
                        tmpList.Add(i);
                    }

                    i++;
                    if (i < n)
                    {
                        // 扩大切片范围 (零分配)
                        // Slice(start, length)
                        frag = sentence.Slice(k, i - k + 1);
                    }
                }

                if (tmpList.Count == 0)
                {
                    tmpList.Add(k);
                }

                dag[k] = tmpList;
            }

            return dag;
        }


        internal IDictionary<int, Pair<int>> Calc(ReadOnlySpan<char> sentence, IDictionary<int, List<int>> dag)
        {
            var n = sentence.Length;
            var route = new Dictionary<int, Pair<int>>();
            route[n] = new Pair<int>(0, 0.0);

            var logtotal = WordDict.GetLogarithm();
            for (var i = n - 1; i > -1; i--)
            {
                var candidate = new Pair<int>(-1, double.NegativeInfinity);
                foreach (int x in dag[i])
                {
                    var slice = sentence.Slice(i, x + 1 - i);
                    var seq = WordDict.TryGetValue(slice, out var s) ? s : 1;
                    var freq = Math.Log(seq) - logtotal + route[x + 1].Freq;
                    if (candidate.Freq < freq)
                    {
                        candidate.Freq = freq;
                        candidate.Key = x;
                    }
                }
                route[i] = candidate;
            }
            return route;
        }
        internal IEnumerable<WordInfo> CutWordsCore(string text, ICutMethodsApi method_api, Regex reHan, Regex reSkip, bool cutAll)
        {
            var result = new List<WordInfo>();
            var blocks = reHan.Split(text);
            var start = 0;

            foreach (var blk in blocks)
            {
                if (string.IsNullOrEmpty(blk))
                {
                    // 空字符串，跳过并不改变 start
                    continue;
                }
                if (reHan.IsMatch(blk))
                {
                    foreach (var word in method_api.Cut(blk))
                    {
                        result.Add(new WordInfo(word, start));
                        start += word.Length;
                    }
                }
                else
                {
                    var tmp = reSkip.Split(blk);
                    foreach (var x in tmp)
                    {
                        if (reSkip.IsMatch(x))
                        {
                            result.Add(new WordInfo(x, start));
                            start += x.Length;
                        }
                        else if (!cutAll)
                        {
                            foreach (var ch in x)
                            {
                                result.Add(new WordInfo(ch.ToString(), start));
                                start += 1;
                            }
                        }
                        else
                        {
                            result.Add(new WordInfo(x, start));
                            start += x.Length;
                        }
                    }
                }
            }

            return result;
        }

        #endregion

        #region Extend Main Dict

        /// <summary>
        /// Loads user dictionaries.
        /// </summary>
        /// <param name="userDictFile"></param>
        public void LoadUserDict(string userDictFile)
        {
            var dictFullPath = Path.GetFullPath(userDictFile);
            Debug.WriteLine("Initializing user dictionary: " + userDictFile);

            lock (locker)
            {
                if (LoadedPath.Contains(dictFullPath))
                    return;

                try
                {
                    var startTime = DateTime.Now.Millisecond;

                    var lines = FileExtension.LoadLines(dictFullPath);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var tokens = RegexUserDict.Match(line.Trim()).Groups;
                        var word = tokens["word"].Value.Trim();
                        var freq = tokens["freq"].Value.Trim();
                        var tag = tokens["tag"].Value.Trim();

                        var actualFreq = freq.Length > 0 ? int.Parse(freq) : 0;
                        AddWord(word, actualFreq, tag);
                    }

                    Debug.WriteLine("user dict '{0}' load finished, time elapsed {1} ms",
                        dictFullPath, DateTime.Now.Millisecond - startTime);
                }
                catch (IOException e)
                {
                    Debug.Fail(string.Format("'{0}' load failure, reason: {1}", dictFullPath, e.Message));
                }
                catch (FormatException fe)
                {
                    Debug.Fail(fe.Message);
                }
            }
        }

        public void AddWord(string word, int freq = 0, string tag = null)
        {
            if (freq <= 0)
            {
                freq = WordDict.SuggestFreq(word, CutWords(word, hmm: false));
            }
            WordDict.AddWord(word, freq);
            // Add user word tag of POS
            if (!string.IsNullOrEmpty(tag))
            {
                UserWordTagTab[word] = tag;
            }
        }

        public void DeleteWord(string word)
        {
            WordDict.DeleteWord(word);
        }

        #endregion

        #region Private Helpers

        internal void AddBufferToWordList(List<string> words, string buf)
        {
            if (buf.Length == 1)
            {
                words.Add(buf);
            }
            else
            {
                if (!WordDict.ContainsWord(buf))
                {
                    var tokens = FinalSeg.Cut(buf);
                    words.AddRange(tokens);
                }
                else
                {
                    for (int i = 0; i < buf.Length; i++)
                    {
                        words.Add(buf.Substring(i, 1));
                    }
                }
            }
        }

        #endregion
    }

    public enum TokenizerMode
    {
        Default,
        Search
    }

}
