using System.IO.Compression;
using System.Xml.Linq;
namespace DocumentService;

public static class ReportWorkbook
{
    // Inline strings cannot execute Excel formulas, including =,+,-,@ and tab prefixes.
    public static byte[] Create(IncompleteReport report)
    {
        XNamespace n="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XElement Row(IEnumerable<string> values)=>new(n+"row",values.Select(v=>new XElement(n+"c",new XAttribute("t","inlineStr"),new XElement(n+"is",new XElement(n+"t",new XAttribute(XNamespace.Xml+"space","preserve"),SafeXml(v))))));
        var rows=new List<XElement>{Row(["Department","HasAttachment","RegistrationNumber","RegisteredDate","IssueDate","Originator","Status","RecipientList","Link"])};
        rows.AddRange(report.Items.Select(x=>Row([x.DepartmentName,x.HasAttachment?"Yes":"No",x.RegistrationNumber,x.RegisteredDate.ToString("yyyy-MM-dd"),x.IssueDate?.ToString("yyyy-MM-dd")??"",x.Originator.ToString(),x.Status,string.Join("; ",x.RecipientList),"/vi/apps/documents/"+x.DocumentId])));
        using var output=new MemoryStream();
        using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))
        {
            void Add(string path,string content){using var writer=new StreamWriter(zip.CreateEntry(path).Open());writer.Write(content);}
            Add("[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Add("_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Add("xl/workbook.xml","<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Incomplete\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add("xl/_rels/workbook.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Add("xl/worksheets/sheet1.xml",new XElement(n+"worksheet",new XElement(n+"sheetData",rows)).ToString());
        }
        return output.ToArray();
    }
    private static string SafeXml(string value)=>new(value.Where(c=>System.Xml.XmlConvert.IsXmlChar(c)).ToArray());
}
