using System;
using System.IO;
using System.Reflection;

namespace MeaninglessAboutDialog.Common
{
    /// <summary>
    /// このアプリケーションに関するアセンブリ情報を参照するユーティリティ クラスです。
    /// </summary>
    internal static class AssemblyInfo
    {
        #region コンストラクタ

        /// <summary>
        /// <see cref="AssemblyInfo"/> の静的コンストラクタ。
        /// </summary>
        static AssemblyInfo()
        {
            Assembly a = Assembly.GetExecutingAssembly();

            Title = ToCustomAttribute<AssemblyTitleAttribute>(a).Title;
            Description = ToCustomAttribute<AssemblyDescriptionAttribute>(a).Description;
            Product = ToCustomAttribute<AssemblyProductAttribute>(a).Product;
            Copyright = ToCustomAttribute<AssemblyCopyrightAttribute>(a).Copyright;
            Version = a.GetName().Version;
            ApplicationPath = a.Location;
            DirectoryPath = Path.GetDirectoryName(a.Location);
        }

        #endregion

        #region メソッド

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assembly"></param>
        /// <returns></returns>
        private static T ToCustomAttribute<T>(Assembly assembly)
            where T : Attribute
        {
            return (T)Attribute.GetCustomAttribute(assembly, typeof(T));
        }

        #endregion

        #region プロパティ

        /// <summary>
        /// アセンブリのタイトル情報を取得します。
        /// </summary>
        public static string Title { get; private set; }

        /// <summary>
        /// アセンブリの説明情報を取得します。
        /// </summary>
        public static string Description { get; private set; }

        /// <summary>
        /// 製品名情報を取得します。
        /// </summary>
        public static string Product { get; private set; }

        /// <summary>
        /// 著作権情報を取得します。
        /// </summary>
        public static string Copyright { get; private set; }

        /// <summary>
        /// アセンブリのバージョン情報を取得します。
        /// </summary>
        public static Version Version { get; private set; }

        /// <summary>
        /// アプリケーションのパスを取得します。
        /// </summary>
        public static string ApplicationPath { get; private set; }

        /// <summary>
        /// アプリケーションが格納されているディレクトリを取得します。
        /// </summary>
        public static string DirectoryPath { get; private set; }

        #endregion
    }
}
