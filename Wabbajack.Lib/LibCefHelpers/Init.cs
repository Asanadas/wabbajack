﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using CefSharp;
using CefSharp.OffScreen;
using Wabbajack.Common;

namespace Wabbajack.Lib.LibCefHelpers
{
    public static class Helpers
    {
        /// <summary>
        /// We bundle the cef libs inside the .exe, we need to extract them before loading any wpf code that requires them
        /// </summary>
        private static void ExtractLibs()
        {
            if (File.Exists("cefsharp.7z") && File.Exists("libcef.dll")) return;

            using (var fs = File.Open("cefsharp.7z", System.IO.FileMode.Create))
            using (var rs = Assembly.GetExecutingAssembly().GetManifestResourceStream("Wabbajack.Lib.LibCefHelpers.cefsharp.7z"))
            {
                rs.CopyTo(fs);
                Utils.Log("Extracting libCef files");
            }
            using (var wq = new WorkQueue(1))
            {
                FileExtractor.ExtractAll(wq, "cefsharp.7z", ".").Wait();
            }
        }

        static Helpers()
        {
            ExtractLibs();
            //if (!Cef.IsInitialized)
            //    Cef.Initialize(new CefSettings { MultiThreadedMessageLoop = true });
        }

        public static HttpClient GetClient(IEnumerable<Cookie> cookies, string referer)
        {
            var container = ToCookieContainer(cookies);
            var handler = new HttpClientHandler { CookieContainer = container };
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Referrer = new Uri(referer);
            return client;
        }

        private static CookieContainer ToCookieContainer(IEnumerable<Cookie> cookies)
        {
            var container = new CookieContainer();
            cookies
                .Do(cookie =>
                {
                    container.Add(new System.Net.Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
                });

            return container;
        }

        public static async Task<Cookie[]> GetCookies(string domainEnding)
        {
            var manager = Cef.GetGlobalCookieManager();
            var visitor = new CookieVisitor();
            if (!manager.VisitAllCookies(visitor))
                return new Cookie[0];
            var cc = await visitor.Task;

            return (await visitor.Task).Where(c => c.Domain.EndsWith(domainEnding)).ToArray();
        }

        private class CookieVisitor : ICookieVisitor
        {
            TaskCompletionSource<List<Cookie>> _source = new TaskCompletionSource<List<Cookie>>();
            public Task<List<Cookie>> Task => _source.Task;

            public List<Cookie> Cookies { get; } = new List<Cookie>();
            public void Dispose()
            {
                _source.SetResult(Cookies);
            }

            public bool Visit(CefSharp.Cookie cookie, int count, int total, ref bool deleteCookie)
            {
                Cookies.Add(new Cookie
                {
                    Name = cookie.Name,
                    Value = cookie.Value,
                    Domain = cookie.Domain,
                    Path = cookie.Path
                });
                if (count == total)
                    _source.SetResult(Cookies);
                deleteCookie = false;
                return true;
            }
        }

        public class Cookie
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Domain { get; set; }
            public string Path { get; set; }
        }

        public static void Init()
        {
            // does nothing, but kicks off the static constructor
        }
    }

    public static class ModuleInitializer
    {
        public static void Initialize()
        {
            Helpers.Init();
        }
    }
}
