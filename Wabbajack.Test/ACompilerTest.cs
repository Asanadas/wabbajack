﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack.Test
{
    public abstract class  ACompilerTest
    {
        public TestContext TestContext { get; set; }
        protected TestUtils utils { get; set; }

        [TestInitialize]
        public void TestInitialize()
        {
            Consts.TestMode = true;

            utils = new TestUtils();
            utils.GameName = "Skyrim Special Edition";

            Utils.LogMessages.Subscribe(f => TestContext.WriteLine(f));

        }

        [TestCleanup]
        public void TestCleanup()
        {
            utils.Dispose();
        }

        protected async Task<Compiler> ConfigureAndRunCompiler(string profile)
        {
            var compiler = MakeCompiler();
            compiler.MO2Profile = profile;
            compiler.ShowReportWhenFinished = false;
            Assert.IsTrue(await compiler.Compile());
            return compiler;
        }

        protected Compiler MakeCompiler()
        {
            var compiler = new Compiler(utils.MO2Folder);
            return compiler;
        }
        protected async Task<ModList> CompileAndInstall(string profile)
        {
            var compiler = await ConfigureAndRunCompiler(profile);
            Install(compiler);
            return compiler.ModList;
        }

        protected async Task Install(Compiler compiler)
        {
            var modlist = Installer.LoadFromFile(compiler.ModListOutputFile);
            var installer = new Installer(compiler.ModListOutputFile, modlist, utils.InstallFolder);
            installer.DownloadFolder = utils.DownloadsFolder;
            installer.GameFolder = utils.GameFolder;
            await installer.Install();
        }
    }
}
