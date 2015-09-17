using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

/// <summary>
/// Handler class for any SOAP webservice.
/// </summary>
public class WebserviceHandler {
	/// <summary>
	/// Where to put the request/response XML log files.
	/// </summary>
	public string LogPath { get; set; }

	/// <summary>
	/// URL endpoint to the service.
	/// </summary>
	public string ServiceUrl { get; set; }

	/// <summary>
	/// SSL certificate to attach to every outgoing call.
	/// </summary>
	public string SslCertFilename { get; set; }

	/// <summary>
	/// SSL certificate password.
	/// </summary>
	public string SslCertPassword { get; set; }

	/// <summary>
	/// User agent to use for each call.
	/// </summary>
	public string UserAgent { get; set; }

	/// <summary>
	/// Setup a new instance of the handler class.
	/// </summary>
	/// <param name="serviceUrl">URL endpoint to the service.</param>
	/// <param name="sslCertFilename">SSL certificate to attach to every outgoing call.</param>
	/// <param name="sslCertPassword">SSL certificate password.</param>
	/// <param name="logPath">Where to put the request/response XML log files.</param>
	/// <param name="userAgent">User agent to use for each call.</param>
	public WebserviceHandler(string serviceUrl, string sslCertFilename, string sslCertPassword, string logPath, string userAgent) {
		this.ServiceUrl = serviceUrl;
		this.SslCertFilename = sslCertFilename;
		this.SslCertPassword = sslCertPassword;
		this.LogPath = logPath;
		this.UserAgent = userAgent;
	}

	/// <summary>
	/// Send the given XML to the service endpoint.
	/// </summary>
	/// <param name="xml">XML document to send.</param>
	/// <param name="method">HTTP method to send with.</param>
	/// <param name="functionName">Name of function. (only for logging purposes)</param>
	/// <returns>Response XML, if any.</returns>
	public XDocument Call(XDocument xml, string method = "POST", string functionName = null) {
		return
			Call(
				xml.ToString(),
				method,
				functionName);
	}

	/// <summary>
	/// Send the given XML to the service endpoint.
	/// </summary>
	/// <param name="xml">XML to send.</param>
	/// <param name="method">HTTP method to send with.</param>
	/// <param name="functionName">Name of function. (only for logging purposes)</param>
	/// <returns>Response XML, if any.</returns>
	public XDocument Call(string xml, string method = "POST", string functionName = null) {
		var request = WebRequest.Create(this.ServiceUrl) as HttpWebRequest;

		if (request == null)
			throw new Exception("WebRequest.Create returned NULL for " + this.ServiceUrl);

		var buffer = Encoding.UTF8.GetBytes(xml);

		request.Method = method;
		request.ContentLength = buffer.Length;
		request.ContentType = "text/xml;charset=utf-8";

		if (this.UserAgent != null)
			request.UserAgent = this.UserAgent;

		if (this.SslCertFilename != null &&
		    File.Exists(this.SslCertFilename)) {
			if (this.SslCertPassword != null)
				request.ClientCertificates.Add(
					new X509Certificate2(
						this.SslCertFilename,
						this.SslCertPassword,
						X509KeyStorageFlags.MachineKeySet));
			else
				request.ClientCertificates.Add(
					new X509Certificate2(
						this.SslCertFilename));
		}

		var reqStream = request.GetRequestStream();

		reqStream.Write(buffer, 0, buffer.Length);
		reqStream.Close();

		if (this.LogPath != null)
			logXML(
				xml,
				functionName);

		XDocument resXml;

		try {
			var response = request.GetResponse() as HttpWebResponse;

			if (response == null)
				throw new Exception("Request returned NULL response!");

			resXml = XDocument.Load(response.GetResponseStream());
		}
		catch (WebException ex) {
			var response = ex.Response as HttpWebResponse;

			if (response == null)
				throw new Exception("Request returned NULL response!");

			resXml = XDocument.Load(response.GetResponseStream());
		}

		if (this.LogPath != null)
			logXML(
				resXml.ToString(),
				functionName,
				false);

		return resXml;
	}

	/// <summary>
	/// Write XML to disk.
	/// </summary>
	/// <param name="xml">XML to write.</param>
	/// <param name="functionName">Name of function to include in the filename.</param>
	/// <param name="isRequest">Whether the given XML comes from the request or response.</param>
	private void logXML(string xml, string functionName, bool isRequest = true) {
		var filename =
			"WebserviceHandler-log-" +
			(isRequest ? "request-" : "response-") +
			(functionName ?? "unknown") + "-" +
			DateTime.Now.Year + "-" +
			pad(DateTime.Now.Month) + "-" +
			pad(DateTime.Now.Day) + "-" +
			pad(DateTime.Now.Hour) + "-" +
			pad(DateTime.Now.Minute) + "-" +
			pad(DateTime.Now.Second) + "-" +
			DateTime.Now.Ticks +
			".xml";

		var path =
			Path.Combine(
				this.LogPath,
				filename);

		File.WriteAllText(
			path,
			xml);
	}

	/// <summary>
	/// Pad the given number to a certain length.
	/// </summary>
	/// <param name="number">Number to pad.</param>
	/// <param name="length">Total length to end up with.</param>
	/// <param name="padding">What to use as padding.</param>
	/// <param name="fromFront">Whether to pad from the front or add to the back.</param>
	/// <returns>Padded string.</returns>
	private string pad(int number, int length = 2, string padding = "0", bool fromFront = true) {
		var output = number.ToString(CultureInfo.InvariantCulture);

		if (output.Length >= length)
			return output;

		while(output.Length < length)
			if (fromFront)
				output = padding + output;
			else
				output += padding;

		return output;
	}
}