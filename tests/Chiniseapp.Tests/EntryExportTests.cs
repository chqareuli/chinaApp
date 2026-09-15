using System.Text.Json;
using System.Xml.Linq;
using Chiniseapp.Application.Entries;
using Chiniseapp.Infrastructure.Entries;

namespace Chiniseapp.Tests;

public class EntryExportTests
{
    [Fact]
    public void XmlEscapesTextAndPreservesChineseGeorgianAndReferences()
    {
        var entry = new EntryDetail(1,"测<&",null,"Ready",null,"Private editor",DateTime.UtcNow,DateTime.UtcNow,1,[],new(true,[]))
        {
            Pinyins = [new(1,"cè",0)], ComplexForms = [new(1,"測",0)],
            Senses = [new(1,"ტესტი & <ტექსტი>",[new("你好","გამარჯობა")]) { Id = 3,
                CrossReferences = [new(4,"see","参见","中国",2,null,null,"Attached")] }]
        };
        var xml = EntryExportSerializer.Xml([entry]);
        var document = XDocument.Parse(xml);
        XNamespace ns = "http://www.tei-c.org/ns/1.0";
        Assert.Equal("ტესტი & <ტექსტი>", document.Descendants(ns+"def").Single().Value);
        Assert.Equal("#entry-2", document.Descendants(ns+"ref").Single().Attribute("target")!.Value);
        Assert.Contains("გამარჯობა",xml);
        Assert.DoesNotContain("Private editor",xml);
        var json = JsonSerializer.Serialize(EntryExportSerializer.DictionaryContent(entry));
        Assert.DoesNotContain("Private editor",json);
        Assert.DoesNotContain("Capabilities",json);
    }
}
