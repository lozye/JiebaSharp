namespace JiebaNet.Segmenter
{
    public struct Token
    {
        public string Word { get; private set; }
        public int StartIndex { get; private set; }
        public int EndIndex { get; private set; }

        public Token(string word, int startIndex, int endIndex)
        {
            Word = word;
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public override string ToString()
        {
            return string.Format("[{0}, ({1}, {2})]", Word, StartIndex, EndIndex);
        }
    }
}