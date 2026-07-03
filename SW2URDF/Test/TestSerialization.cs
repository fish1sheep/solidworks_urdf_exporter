using SolidWorks.Interop.sldworks;
using SW2URDF.URDF;
using SW2URDF.URDFExport;
using Xunit;

namespace SW2URDF.Test
{
    [Collection("Requires SW Test Collection")]
    public class TestSerialization : SW2URDFTest
    {
        public TestSerialization(SWTestFixture fixture) : base(fixture)
        {
        }

        [Theory]
        [InlineData("3_DOF_ARM", 4)]
        public void TestLoadConfigFromStringXML(string modelName, int expNumLinks)
        {
            ModelDoc2 doc = OpenSWDocument(modelName);
            SolidWorks.Interop.sldworks.Attribute swAtt =
                ConfigurationSerialization.FindSWSaveAttribute(doc, "URDF Export Configuration");
            Xunit.Assert.NotNull(swAtt);
            Parameter param = swAtt.GetParameter("data");

            Xunit.Assert.NotNull(param);
            string data = param.GetStringValue();

            Xunit.Assert.NotNull(data);
            Xunit.Assert.NotEmpty(data);

            LinkNode baseNode = ConfigurationSerialization.LoadConfigFromStringXML(data);
            Link link = baseNode.RebuildLink();
            Xunit.Assert.Equal(expNumLinks, CommonSwOperations.GetCount(link));
        }

        [Theory]
        [InlineData("3_DOF_ARM", 4)]
        [InlineData("4_WHEELER", 5)]
        public void TestLoadBaseNodeFromModel(string modelName, int expNumLinks)
        {
            ModelDoc2 doc = OpenSWDocument(modelName);
            LinkNode baseNode = ConfigurationSerialization.LoadBaseNodeFromModel(doc, out bool error);
            Xunit.Assert.False(error);
            Xunit.Assert.NotNull(baseNode);
            Xunit.Assert.Equal(expNumLinks, CommonSwOperations.GetCount(baseNode.RebuildLink()));
        }

        [Theory]
        [InlineData("3_DOF_ARM")]
        [InlineData("4_WHEELER")]
        public void TestSerializeToString(string modelName)
        {
            ModelDoc2 doc = OpenSWDocument(modelName);
            LinkNode baseNode = ConfigurationSerialization.LoadBaseNodeFromModel(doc, out bool error);
            Xunit.Assert.False(error);

            string newData = ConfigurationSerialization.SerializeToString(baseNode);
            Xunit.Assert.NotNull(newData);
            Xunit.Assert.NotEmpty(newData);

        }
    }
}