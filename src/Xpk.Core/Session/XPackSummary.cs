using System.Collections.Generic;

namespace Xpk.Core.Session
{
    public sealed class XPackSummary
    {
        public string Name { get; set; }
        public string Folder { get; set; }
        public string Author { get; set; }
        public string Date { get; set; }
        public string Description { get; set; }
        public int Objects { get; set; }
        public int SObjects { get; set; }
        public int Materials { get; set; }
        public int Textures { get; set; }
        public int CrossSections { get; set; }
        public string RaceBitsSha256 { get; set; }
        public bool ReadOnly { get; set; }
        public string ReadOnlyReason { get; set; }

        /// <summary>Reference errors from RaceBits' Verify* calls and version warnings. Loading continues despite these.</summary>
        public IReadOnlyList<string> Warnings { get; set; }
    }
}
