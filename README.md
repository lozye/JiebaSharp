# JiebaSharp

## 基于
forked from [SilentCC/JIEba-netcore](https://github.com/SilentCC/JIEba-netcore)

## NUGET
+ [nuget](https://www.nuget.org/packages/JiebaSharp/)

## 修改项
+ 修改了 `WordDictionary` 支持了不同分词实例加载不同字典 [issues](https://github.com/anderscui/jieba.NET/issues/91)
+ 修改了 `FileExtension` 可通过 `FileExtension.Provider` 修改词典文件管理器，并修改了JSON序列化工具
+ 修改了 `JiebaAnalyzer` 简化使用
+ 项目文件中新增 `<DefineConstants>LuceneSharp;</DefineConstants>` ，用以控制是否引用 `Lucene.Net` 和编译 `JiebaAnalyzer`
+ 词典使用了占用内存较小的词典文件 https://github.com/fxsjy/jieba/raw/master/extra_dict/dict.txt.small


## 附录
+ [词典](https://github.com/fxsjy/jieba)