using NUnit.Framework;
using Geotagger_V2;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System;

namespace GeotaggerTest
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void testGetInspector()
        {
            string[] inspector = { "", "Matt Wynyard", "Ian Nobel", null };
            string[] expected = { "", "", "IN", "" };
            List<bool> results = new List<bool>();
            int i = 0;
            foreach (var item in inspector)
            {
                string ins = Utilities.getInspector(inspector[i]);
                try
                {
                    Assert.AreEqual(expected[i], ins);
                    results.Add(true);
                }
                catch (AssertionException ex)
                {
                    results.Add(false);
                }
                    
                i++;
            }
            foreach( var item in results)
            {
                Console.WriteLine(item);
            }
        }
    }
}