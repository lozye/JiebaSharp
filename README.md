# JiebaSharp

## 基于
forked from [SilentCC/JIEba-netcore](https://github.com/SilentCC/JIEba-netcore)

## NUGET
+ [nuget](https://www.nuget.org/packages/JiebaSharp/)

## 修改项
+ 修改了 `WordDictionary` 支持了不同分词实例加载不同字典 [issues](https://github.com/anderscui/jieba.NET/issues/91)
+ 修改了 `FileExtension` 可通过 `FileExtension.Provider` 修改词典文件管理器，并修改了JSON序列化工具
+ 修改了 `JiebaAnalyzer` 简化使用
+ 项目文件中新增 `DefineConstants` ，用以控制编译和资源文件的引用
+ 词典使用了占用内存较小的词典文件 https://github.com/fxsjy/jieba/raw/master/extra_dict/dict.txt.small

## 编译变量
通过修改 `JiebaSharp.csproj` 文件中的 `DefineConstants` 项来调整编译变量 默认编译变量 `<DefineConstants>LuceneSharp;SmallDict</DefineConstants>`

+ LuceneSharp [*] 是否引用 `Lucene.Net` 和编译 `JiebaAnalyzer`
+ TfidfAndPosSeg 是否编译 `TfidfExtractor` 和 `PosSegmenter` 以及相关资源文件
+ SmallDict [*] 是否使用小词典
+ BigDict 是否使用大词典

## 附录
+ [词典](https://github.com/fxsjy/jieba)