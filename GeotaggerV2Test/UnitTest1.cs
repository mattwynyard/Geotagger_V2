using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Geotagger_V2;
using System.Collections.Generic;

namespace GeotaggerV2Test
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void testGetInspector()
        {
            string[] inspector = { "", "Matt Wynyard", "Ian Nobel", null };
            string[] expected = { "", "", "IN", "" };
            List<bool> results = new List<bool>();
            int i = 0;
            foreach (var item in inspector)
            {
                string ins = Utilities.getInspector(inspector[i]);
                Console.WriteLine(ins);
                Assert.AreEqual(expected[i], ins);
                results.Add(true);
            }
 
        }
    }
}
