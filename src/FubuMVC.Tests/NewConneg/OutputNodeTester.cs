using System.Linq;
using FubuMVC.Core.Resources.Conneg.New;
using FubuMVC.Core.Runtime.Formatters;
using FubuMVC.Core.View;
using FubuTestingSupport;
using HtmlTags;
using NUnit.Framework;
using Rhino.Mocks;

namespace FubuMVC.Tests.NewConneg
{
    [TestFixture]
    public class OutputNodeTester
    {
        [Test]
        public void ClearAll()
        {
            var node = new OutputNode(typeof (Address));
            node.UsesFormatter<XmlFormatter>();
            node.UsesFormatter<JsonFormatter>();
        
            node.ClearAll();

            node.Writers.Any().ShouldBeFalse();
        }


        [Test]
        public void JsonOnly_from_scratch()
        {
            var node = new OutputNode(typeof(Address));
            node.JsonOnly();

            node.Writers.ShouldHaveCount(1);
            node.UsesFormatter<JsonFormatter>().ShouldBeTrue();
        }

        [Test]
        public void JsonOnly_with_existing_stuff()
        {
            var node = new OutputNode(typeof(Address));
            node.UsesFormatter<XmlFormatter>();

            node.JsonOnly();

            node.Writers.ShouldHaveCount(1);
            node.UsesFormatter<JsonFormatter>().ShouldBeTrue();
        }

        [Test]
        public void UsesFormatter()
        {
            var node = new OutputNode(typeof(Address));

            node.UsesFormatter<XmlFormatter>().ShouldBeFalse();
            node.UsesFormatter<JsonFormatter>().ShouldBeFalse();

            node.AddFormatter<XmlFormatter>();

            node.UsesFormatter<XmlFormatter>().ShouldBeTrue();
            node.UsesFormatter<JsonFormatter>().ShouldBeFalse();

            node.AddFormatter<JsonFormatter>();

            node.UsesFormatter<XmlFormatter>().ShouldBeTrue();
            node.UsesFormatter<JsonFormatter>().ShouldBeTrue();
        }

        [Test]
        public void add_formatter()
        {
            var node = new OutputNode(typeof (Address));
            node.AddFormatter<JsonFormatter>();

            node.Writers.Single()
                .ShouldEqual(new WriteWithFormatter(typeof (Address), typeof (JsonFormatter)));
        }

        [Test]
        public void add_html_to_the_end()
        {
            var node = new OutputNode(typeof (HtmlTag));
            var html = node.AddHtml();

            node.Writers.Single().ShouldBeOfType<WriteHtml>()
                .ResourceType.ShouldEqual(typeof (HtmlTag));
        }

        [Test]
        public void add_html_to_the_end_is_idempotent()
        {
            var node = new OutputNode(typeof (HtmlTag));
            var html = node.AddHtml();
            node.AddHtml();
            node.AddHtml();
            node.AddHtml();
            node.AddHtml();


            node.Writers.Single().ShouldBeOfType<WriteHtml>()
                .ResourceType.ShouldEqual(typeof (HtmlTag));
        }

        [Test]
        public void add_writer_happy_path()
        {
            var node = new OutputNode(typeof (Address));
            var writer = node.AddWriter<FakeAddressWriter>();

            node.Writers.Single().ShouldBeTheSameAs(writer);

            writer.ResourceType.ShouldEqual(typeof (Address));
            writer.WriterType.ShouldEqual(typeof (FakeAddressWriter));
        }

        [Test]
        public void adding_a_formatter_is_idempotent()
        {
            var node = new OutputNode(typeof (Address));
            node.AddFormatter<JsonFormatter>();
            node.AddFormatter<JsonFormatter>();
            node.AddFormatter<JsonFormatter>();
            node.AddFormatter<JsonFormatter>();

            node.Writers.Single()
                .ShouldEqual(new WriteWithFormatter(typeof (Address), typeof (JsonFormatter)));
        }

        [Test]
        public void add_view()
        {
            var node = new OutputNode(typeof(Address));
            var viewToken = MockRepository.GenerateMock<IViewToken>();

            var viewNode = node.AddView(viewToken);
            node.Writers.ShouldContain(viewNode);

            viewNode.View.ShouldBeTheSameAs(viewToken);
        }
    }
}