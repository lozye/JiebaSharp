using JiebaNet.Segmenter.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace JiebaNet
{
    internal class WordDictionary : IEnumerable<KeyValuePair<string, int>>
    {
        private IDictionary<string, int> _main;
        private double _total;
        private int _locked;
        private readonly object _locker = new object();

        public WordDictionary New() => new WordDictionary { _main = Instance._main, _total = Instance._total };
        private WordDictionary(bool is_main)
        {
            _locked = 1;
            _main = new Dictionary<string, int>();
            load_dict();
        }
        private WordDictionary() { }
        private static readonly Lazy<WordDictionary> lazyInstance = new Lazy<WordDictionary>(() => new WordDictionary(true));
        /// <summary>
        /// 通过WordDictionary.Instance修改全局，否则在JiebaSegmenter中修改
        /// </summary>
        public static WordDictionary Instance => lazyInstance.Value;
        private void load_dict()
        {
            try
            {
                var stopWatch = new Stopwatch();
                stopWatch.Start();
                var lines = FileExtension.LoadLines(ConfigManager.MainDictFile);

                foreach (var line in lines)
                {
                    var tokens = line.Split(' ');
                    if (tokens.Length < 2)
                    {
                        Debug.Fail(string.Format("Invalid line: {0}", line));
                        continue;
                    }

                    var word = tokens[0];
                    var freq = int.Parse(tokens[1]);

                    _main[word] = freq;
                    _total += freq;

                    foreach (var ch in Enumerable.Range(0, word.Length))
                    {
                        var wfrag = word.Sub(0, ch + 1);
                        if (!_main.ContainsKey(wfrag)) _main[wfrag] = 0;
                    }
                }
                stopWatch.Stop();
                Debug.WriteLine("main dict load finished, time elapsed {0} ms", stopWatch.ElapsedMilliseconds);
            }
            catch (IOException e)
            {
                Debug.Fail(string.Format("{0} load failure, reason: {1}", _main, e.Message));
            }
            catch (FormatException fe)
            {
                Debug.Fail(fe.Message);
            }
        }
        /// <summary>
        /// 获取_total的对数
        /// </summary>
        /// <returns></returns>
        public double GetLogarithm() => Math.Log(_total);
        public bool ContainsWord(string word) => _main.ContainsKey(word);
        public bool TryGetValue(string key, out int value) => _main.TryGetValue(key, out value);
        /// <summary>
        /// 新增复制机制，如果没修改过用户字典则直接使用_main对象，否则复制一个新的字典
        /// </summary>
        private void EnsureLocked()
        {
            if (_locked == 1) return;
            lock (_locker)
            {
                if (_locked != 1)
                {
                    var _temp = new Dictionary<string, int>(_main);
                    _main = _temp;
                    _locked = 1;
                }
            }
        }
        public void AddWord(string word, int freq, string tag = null)
        {
            EnsureLocked();

            if (TryGetValue(word, out var f)) { _total -= f; }
            _main[word] = freq;
            _total += freq;

            for (var i = 0; i < word.Length; i++)
            {
                var wfrag = word.Substring(0, i + 1);
                if (!_main.ContainsKey(wfrag)) _main[wfrag] = 0;
            }
        }
        public void DeleteWord(string word) => AddWord(word, 0);
        public int GetFreqOrDefault(string key) => TryGetValue(key, out int value) ? value : 1;
        public int SuggestFreq(string word, IEnumerable<string> segments)
        {
            double freq = 1;
            foreach (var seg in segments)
            {
                freq *= GetFreqOrDefault(seg) / _total;
            }
            return Math.Max((int)(freq * _total) + 1, GetFreqOrDefault(word));
        }
        public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => _main.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
