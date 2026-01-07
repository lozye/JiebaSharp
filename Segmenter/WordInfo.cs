namespace JiebaNet.Segmenter
{
    public struct WordInfo
    {
        public WordInfo(string value, int position)
        {
            this.value = value;
            this.position = position;
        }
        //分词的内容
        public string value { get; private set; }
        //分词的初始位置
        public int position { get; private set; }
    }
}
