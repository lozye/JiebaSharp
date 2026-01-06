using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace JiebaNet
{
    public class FileExtension
    {
        /// <summary>
        /// 文件管理器
        /// </summary>
        public static IFileProvider Provider = new PhysicalFileProvider(Path.Combine(AppDomain.CurrentDomain.BaseDirectory));

        public static string LoadString(string path)
        {
            var info = Provider.GetFileInfo(path);
            if (!info.Exists) return "";
            using var st = info.CreateReadStream();
            using var sr = new StreamReader(st);
            return sr.ReadToEnd();
        }

        public static IEnumerable<string> LoadLines(string path)
        {
            using StringReader sr = new StringReader(LoadString(path));
            string line = null;
            while ((line = sr.ReadLine()) != null)
                yield return line;

        }
        public static T LoadString<T>(string path) where T : new()
        {
            var text = LoadString(path);
            if (string.IsNullOrEmpty(path)) return new T();
            return JsonSerializer.Deserialize<T>(text);
        }
    }
}

// 空命名空间保障编译
namespace Microsoft.Extensions.FileProviders { }