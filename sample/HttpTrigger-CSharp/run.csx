#r "..\SharedBin\Microsoft.ReportViewer.Common.dll"
#r "..\SharedBin\Microsoft.ReportViewer.ProcessingObjectModel.dll"
#r "..\SharedBin\Microsoft.ReportViewer.WebForms.dll"

#r "Newtonsoft.Json"
#r "System.Data"
#r "System.Web"
#r "System.Net"
#r "System.IO"

using Newtonsoft.Json;
using Microsoft.Reporting.WebForms;
using System.Data;
using System;
using System.Web;
using System.Web.Hosting;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.IO;

public static HttpResponseMessage Run(HttpRequestMessage req, TraceWriter log)
{
    ReportViewer rv = new ReportViewer();
    rv.ProcessingMode = ProcessingMode.Local;
    rv.LocalReport.ReportPath = "e:\\Test2.rdlc";
    ReportParameter Test = new ReportParameter("Test", "Test Report!");

    rv.LocalReport.SetParameters(new ReportParameter[] { Test });
    rv.LocalReport.Refresh();

    byte[] streamBytes = null;
    string mimeType = "";
    string encoding = "";
    string filenameExtension = "";
    string[] streamids = null;
    Warning[] warnings = null;

    streamBytes = rv.LocalReport.Render("PDF", null, out mimeType, out encoding, out filenameExtension, out streamids, out warnings);

    HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
    response.Content = new ByteArrayContent(streamBytes);
    response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
    response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
    return response;
}