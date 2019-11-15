﻿using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Lib;
using Wabbajack.Lib.CompilationSteps;

namespace Wabbajack.Test
{
    [TestClass]
    public class CompilationStackTests : ACompilerTest
    {
        [TestMethod]
        public async Task TestStackSerialization()
        {
            var profile = utils.AddProfile();
            var mod = utils.AddMod("test");

            utils.Configure();
            var compiler = await ConfigureAndRunCompiler(profile);
            var stack = compiler.MakeStack();

            var serialized = Serialization.Serialize(stack);
            var rounded = Serialization.Serialize(Serialization.Deserialize(serialized, compiler));

            Assert.AreEqual(serialized, rounded);
            
            Assert.IsNotNull(compiler.GetStack());
        }
    }
}
