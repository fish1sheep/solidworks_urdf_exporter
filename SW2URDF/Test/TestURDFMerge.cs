using SW2URDF.URDF;
using SW2URDF.URDFExport;
using SW2URDF.URDFExport.URDFMerge;
using SW2URDF.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Xunit;

namespace SW2URDF.Test
{
    /// <summary>
    /// Tests for URDF tree merging functionality.
    /// Covers TreeMerger.Merge(), URDFTreeCorrespondance.BuildCorrespondance(),
    /// and URDFTreeCorrespondance.GetCorrespondingLink().
    /// </summary>
    public class TestURDFMerge : SW2URDFTest
    {
        public TestURDFMerge(SWTestFixture fixture) : base(fixture)
        {
        }

        // ==================== MergeLink (via Merge) ====================

        [Fact]
        public void TestMerge_NullCSVReturnsCAD()
        {
            // TreeMerger with all flags false and no CSV correspondance
            // should return the CAD link unchanged (SW components preserved)
            TreeMerger merger = new TreeMerger(false, false, false, false);

            Link cadRoot = CreateTestLink("base_link");
            cadRoot.SWComponents = new List<IComponentHandle>
            {
                new ComponentHandle("comp1"),
                new ComponentHandle("comp2")
            };
            Link child = CreateTestLink("child_joint");
            cadRoot.Children.Add(child);

            TreeView tree = new TreeView();
            TreeViewItem rootItem = CreateTreeItem(cadRoot);
            tree.Items.Add(rootItem);

            // Empty correspondance — no CSV links to merge
            URDFTreeCorrespondance correspondance = new URDFTreeCorrespondance();
            Link merged = merger.Merge(tree, correspondance);

            Assert.NotNull(merged);
            Assert.Equal("base_link", merged.Name);
            Assert.Equal(2, merged.SWComponents.Count);
            Assert.Equal("comp1", merged.SWComponents[0].Name);
            Assert.Equal(1, merged.Children.Count);
        }

        [Fact]
        public void TestMerge_AllFlagsTrueOverwritesFromCSV()
        {
            // CAD link with default values
            Link cadLink = CreateTestLink("test_link");
            cadLink.Inertial.Mass.Value = 1.0;
            cadLink.Visual.Origin.SetXYZ(new double[] { 0, 0, 0 });
            cadLink.Joint.Type = "fixed";
            cadLink.Joint.Limit.Effort = 0;

            // CSV link with different values
            Link csvLink = CreateTestLink("test_link");
            csvLink.Inertial.Mass.Value = 5.0;
            csvLink.Visual.Origin.SetXYZ(new double[] { 0.1, 0.2, 0.3 });
            csvLink.Joint.Type = "revolute";
            csvLink.Joint.Limit.Effort = 100;

            // Build one-item tree
            TreeView tree = new TreeView();
            tree.Items.Add(CreateTreeItem(cadLink));

            // Build correspondance matching the CSV link to the tree item
            URDFTreeCorrespondance correspondance = new URDFTreeCorrespondance();
            List<Link> loadedLinks = new List<Link> { csvLink };
            correspondance.BuildCorrespondance(null, loadedLinks, out _, out _);

            // Merge with all flags true
            TreeMerger merger = new TreeMerger(true, true, true, true);
            Link merged = merger.Merge(tree, correspondance);

            Assert.NotNull(merged);
            // Inertial came from CSV
            Assert.Equal(5.0, merged.Inertial.Mass.Value);
            // Visual origin came from CSV
            Assert.Equal(0.1, merged.Visual.Origin.GetXYZ()[0]);
            // Joint type and limits came from CSV
            Assert.Equal("revolute", merged.Joint.Type);
            Assert.Equal(100, merged.Joint.Limit.Effort);
        }

        // ==================== URDFTreeCorrespondance ====================

        [Fact]
        public void TestBuildCorrespondance_AllUnmatched()
        {
            URDFTreeCorrespondance correspondance = new URDFTreeCorrespondance();

            Link link1 = CreateTestLink("link1");
            Link link2 = CreateTestLink("link2");
            List<Link> loadedLinks = new List<Link> { link1, link2 };

            List<Link> matchedLinks;
            List<Link> unmatchedLinks;

            // Build correspondance with null tree — no items to match against
            correspondance.BuildCorrespondance(null, loadedLinks,
                out matchedLinks, out unmatchedLinks);

            // With null tree, all loaded links should be unmatched
            Assert.Empty(matchedLinks);
            Assert.Equal(2, unmatchedLinks.Count);
        }

        [Fact]
        public void TestGetCorrespondingLink_ReturnsNullWhenNotInTree()
        {
            URDFTreeCorrespondance correspondance = new URDFTreeCorrespondance();
            TreeViewItem unknownItem = new TreeViewItem { Name = "unknown" };

            // No correspondance built — GetCorrespondingLink should return null
            Link result = correspondance.GetCorrespondingLink(unknownItem);
            Assert.Null(result);
        }

        [Fact]
        public void TestMergeLink_PreservesSWComponents()
        {
            // Verify that even when CSV has no components,
            // the CAD link's SW components are preserved
            Link cadLink = CreateTestLink("test_link");
            cadLink.SWComponents = new List<IComponentHandle>
            {
                new ComponentHandle("cad_comp")
            };

            Link csvLink = CreateTestLink("test_link");
            csvLink.SWComponents = new List<IComponentHandle>(); // empty

            TreeView tree = new TreeView();
            tree.Items.Add(CreateTreeItem(cadLink));

            URDFTreeCorrespondance correspondance = new URDFTreeCorrespondance();

            // Merge with all flags false — should keep CAD values
            TreeMerger merger = new TreeMerger(false, false, false, false);
            Link merged = merger.Merge(tree, correspondance);

            Assert.NotNull(merged);
            Assert.Single(merged.SWComponents);
            Assert.Equal("cad_comp", merged.SWComponents[0].Name);
        }

        // ==================== Helper Methods ====================

        /// <summary>
        /// Creates a Link with the given name and default values.
        /// </summary>
        private static Link CreateTestLink(string name)
        {
            Link link = new Link();
            link.Name = name;
            link.SWComponents = new List<IComponentHandle>();
            link.SWMainComponent = null;
            return link;
        }

        /// <summary>
        /// Creates a TreeViewItem wrapping a Link as its Tag.
        /// </summary>
        private static TreeViewItem CreateTreeItem(Link link)
        {
            TreeViewItem item = new TreeViewItem();
            item.Tag = link;
            item.Name = link.Name;
            return item;
        }
    }
}
