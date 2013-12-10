﻿﻿using System;
using System.IO;
using CmisSync.Lib;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text;
using log4net;
using log4net.Config;

namespace TestLibrary
{
    [TestFixture]
    class CmisSyncLibUtilsTest
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CmisSyncLibUtilsTest));
        private static readonly string TestFolderParent = Directory.GetCurrentDirectory();
        private static readonly string TestFolder = Path.Combine(TestFolderParent, "conflicttest");

        [TestFixtureSetUp]
        public void ClassInit()
        {
            log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());
        }

        [SetUp]
        public void TestInit()
        {
            Directory.CreateDirectory(TestFolder);
        }

        [TearDown]
        public void TestCleanup()
        {
            if (Directory.Exists(TestFolder))
            {
                Directory.Delete(TestFolder, true);
            }
        }

        [Test, Category("Fast")]
        public void FindNextFreeFilenameTest()
        {
            string user = "unittest";
            string path = Path.Combine(TestFolder, "testfile.txt");
            string originalParent = Directory.GetParent(path).FullName;
            string conflictFilePath = Utils.FindNextConflictFreeFilename(path, user);
            Assert.AreEqual(path, conflictFilePath, "There is no testfile.txt but another conflict file is created");
            for (int i = 0; i < 10; i++)
            {
                File.Create(conflictFilePath);
                conflictFilePath = Utils.FindNextConflictFreeFilename(path, user);
                Assert.AreNotEqual(path, conflictFilePath, "The conflict file must differ from original file");
                Assert.True(conflictFilePath.Contains(user), "The username should be added to the conflict file name");
                Assert.True(conflictFilePath.EndsWith(Path.GetExtension(path)), "The file extension must be kept the same as in the original file");
//                string filename = Path.GetFileName(conflictFilePath);
                string conflictParent = Directory.GetParent(conflictFilePath).FullName;
                Assert.AreEqual(originalParent, conflictParent, "The conflict file must exists in the same directory like the orignial file");
            }
        }
    }
}
