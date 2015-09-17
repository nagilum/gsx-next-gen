using System;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Configuration;
using System.Xml.Linq;
using System.Xml.Serialization;

public class GSXNextGen {
	public static Hashtable FaultEntries;

	public enum Envelope {
		AM,
		APAC,
		ASP,
		Core,
		EMEA,
		Global,
		IPHONE,
		LA
	}

	/// <summary>
	/// Get an element from the container.
	/// </summary>
	/// <param name="container">XML container to cycle.</param>
	/// <param name="name">Name of element to fetch.</param>
	/// <returns>Element.</returns>
	public static XElement GetElement(XContainer container, string name) {
		return container.Descendants().FirstOrDefault(d => d.Name == name) ??
		       container.Descendants().FirstOrDefault(d => d.Name.ToString().EndsWith(name));
	}

	/// <summary>
	/// Get a value from the container.
	/// </summary>
	/// <param name="container">XML container to cycle.</param>
	/// <param name="name">Name of element to fetch value from.</param>
	/// <returns>Value.</returns>
	public static string GetValue(XContainer container, string name) {
		return container.Descendants()
			.Where(d => string.Equals(d.Name.ToString(), name, StringComparison.InvariantCultureIgnoreCase))
			.Select(d => d.Value)
			.FirstOrDefault();
	}

	/// <summary>
	/// Parse given object and turn it into XML.
	/// </summary>
	/// <param name="wrapper">Object to parse.</param>
	/// <param name="envelope">Type of XML to send.</param>
	/// <returns>XML string.</returns>
	public static string ParseWrapper(object wrapper, Envelope envelope) {
		var xml = new StringBuilder();
		var type = string.Empty;
		var typeURL = string.Empty;

		switch (envelope) {
			case Envelope.AM:
				type = "am";
				typeURL = "core/asp/am";
				break;

			case Envelope.APAC:
				type = "apac";
				typeURL = "core/asp/apac";
				break;

			case Envelope.ASP:
				type = "asp";
				typeURL = "core/asp";
				break;

			case Envelope.Core:
				type = "core";
				typeURL = "core";
				break;

			case Envelope.EMEA:
				type = "emea";
				typeURL = "core/asp/emea";
				break;

			case Envelope.Global:
				type = "glob";
				typeURL = "global";
				break;

			case Envelope.IPHONE:
				type = "iph";
				typeURL = "iphone";
				break;

			case Envelope.LA:
				type = "la";
				typeURL = "core/asp/la";
				break;
		}

		xml
			.Append("<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:" + type + "=\"http://gsxws.apple.com/elements/" + typeURL + "\">")
			.Append("<soapenv:Header/>")
			.Append("<soapenv:Body>");

		buildXML(wrapper, ref xml);

		xml
			.Append("</soapenv:Body>")
			.Append("</soapenv:Envelope>");

		return xml.ToString();
	}

	/// <summary>
	/// 
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="wrapper"></param>
	/// <param name="envelope"></param>
	/// <param name="serviceUrl"></param>
	/// <param name="functionName"></param>
	/// <returns></returns>
	public static T Execute<T>(object wrapper, Envelope envelope, string serviceUrl, string functionName) {
		FaultEntries = null;

		var sslCertFilename = HttpContext.Current.Server.MapPath(WebConfigurationManager.AppSettings["GSX_PROD_CertFilename"]);
		var sslCertPassword = WebConfigurationManager.AppSettings["GSX_PROD_CertPassword"];
		var logPath = HttpContext.Current.Server.MapPath("~/xml/");
		var requestXML = ParseWrapper(wrapper, envelope);

		var wh = new WebserviceHandler(
			serviceUrl,
			sslCertFilename,
			sslCertPassword,
			logPath,
			"AppleCare GSX WebService Wrapper");

		var responseXML = wh.Call(
			requestXML,
			"POST",
			functionName);

		var fault = GetElement(responseXML, "Fault");
		if (fault != null) {
			FaultEntries = new Hashtable();

			foreach (var el in fault.Descendants())
				FaultEntries.Add(
					el.Name.ToString(),
					el.Value);
		}

		var type = typeof (T);
		var element = GetElement(responseXML, type.Name);

		if (element == null)
			return default(T);

		var serializer = new XmlSerializer(typeof(T));
		var reader = new StringReader(element.ToString());
		return (T) serializer.Deserialize(reader);
	}

	/// <summary>
	/// Build XML from the given object.
	/// </summary>
	private static void buildXML(object wrapper, ref StringBuilder xml) {
		if (wrapper == null)
			return;

		var type = wrapper.GetType();
		var name = type.Name;

		NameValueCollection attributes = null;
		var colonCutOffset = 0;

		try {
			var temp = wrapper as Wrapper;

			if (temp != null) {
				if (temp.Attributes != null)
					attributes = temp.Attributes;

				if (temp.ClassName != null)
					name = temp.ClassName;

				colonCutOffset = temp.ColonCutOffset;
			}
		}
		catch {}

		if (colonCutOffset > 0 &&
		    name.Length > colonCutOffset)
			name =
				name.Substring(0, colonCutOffset) + ":" +
				name.Substring(colonCutOffset);

		// Add root tag with possible attributes.
		xml.Append("<" + name);

		if (attributes != null)
			foreach (var key in attributes.AllKeys)
				xml.Append(" " + key + "\"" + attributes[key] + "\"");

		xml.Append(">");

		// Cycle properties for values and children.
		foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
			var obj = property.GetValue(wrapper, null);
			var propertyName = property.Name;

			if (propertyName == "Attributes" ||
			    propertyName == "ColonCutOffset" ||
			    propertyName == "ClassName")
				continue;

			if (obj is string) {
				xml
					.Append("<" + propertyName + ">")
					.Append(obj)
					.Append("</" + propertyName + ">");
			}
			else if (obj is Array) {
				var array = obj as Array;

				foreach (var item in array)
					buildXML(item, ref xml);
			}
			else {
				buildXML(obj, ref xml);
			}
		}

		// End tag.
		xml.Append("</" + name + ">");
	}

	#region Base XML

	public class Wrapper {
		public NameValueCollection Attributes { get; set; }
		public int ColonCutOffset = 0;
		public string ClassName = null;
	}

	public class userSession : Wrapper {
		public string userSessionId { get; set; }
	}

	public class Fault {
		public string faultcode { get; set; }
		public string faultstring { get; set; }
		public detail detail { get; set; }
	}

	public class detail {
		public string operationId { get;set; }
	}

	#endregion

	#region Function Types

	public class Authenticate {
		public static string Perform(string userId = null) {
			const string serviceUrl = "https://gsxapi.apple.com/gsx-ws/services/emea/asp";

			var serviceAccountNo = WebConfigurationManager.AppSettings["GSX_PROD_ServiceAccountNo"];

			if (userId == null)
				userId = WebConfigurationManager.AppSettings["GSX_PROD_UserId"];

			var res = Execute<AuthenticateResponse>(
				new globAuthenticate {
					ColonCutOffset = 4,
					AuthenticateRequest = new AuthenticateRequest {
						languageCode = "EN",
						serviceAccountNo = serviceAccountNo,
						userId = userId,
						userTimeZone = "CET"
					}
				},
				Envelope.Global,
				serviceUrl,
				"Authenticate");

			return
				res != null
					? res.userSessionId
					: null;
		}

		public class globAuthenticate : Wrapper {
			public AuthenticateRequest AuthenticateRequest { get; set; }
		}

		public class AuthenticateRequest : Wrapper {
			public string userId { get; set; }
			public string languageCode { get; set; }
			public string userTimeZone { get; set; }
			public string serviceAccountNo { get; set; }
		}

		public class AuthenticateResponse {
			public string operationId { get; set; }
			public string userSessionId { get; set; }
		}
	}

	public class CreateCarryIn {
		public static CreateCarryInResponse Perform(string userSessionId, repairData repairData) {
			const string serviceUrl = "https://gsxapi.apple.com/gsx-ws/services/emea/asp";

			return Execute<CreateCarryInResponse>(
				new emeaCreateCarryIn {
					ColonCutOffset = 4,
					CreateCarryInRequest = new CreateCarryInRequest {
						userSession = new userSession {
							userSessionId = userSessionId
						},
						repairData = repairData
					}
				},
				Envelope.EMEA,
				serviceUrl,
				"CreateCarryIn");
		}

		public class emeaCreateCarryIn : Wrapper {
			public CreateCarryInRequest CreateCarryInRequest { get; set; }
		}

		public class CreateCarryInRequest : Wrapper {
			public userSession userSession { get; set; }
			public repairData repairData { get; set; }
		}

		public class repairData {
			public string billTo { get; set; }
			public string checkIfOutOfWarrantyCoverage { get; set; }
			public customerAddress customerAddress { get; set; }
			public string diagnosedByTechId { get; set; }
			public string diagnosis { get; set; }
			public string fileName { get; set; }
			public string fileData { get; set; }
			public string isNonReplenished { get; set; }
			public string notes { get; set; }
			public orderLines[] orderLines { get; set; }
			public componentCheckDetails[] componentCheckDetails { get; set; }
			public string overrideDiagnosticCodeCheck { get; set; }
			public string poNumber { get; set; }
			public string popFaxed { get; set; }
			public string referenceNumber { get; set; }
			public string requestReviewByApple { get; set; }
			public string serialNumber { get; set; }
			public string shipTo { get; set; }
			public string symptom { get; set; }
			public string unitReceivedDate { get; set; }
			public string unitReceivedTime { get; set; }
			public string markCompleteFlag { get; set; }
			public string replacementSerialNumber { get; set; }
			public string componentCheckReview { get; set; }
			public string serviceType { get; set; }
			public string shipBox { get; set; }
			public string consumerLawEligible { get; set; }
			public string reportedSymptomCode { get; set; }
			public string reportedIssueCode { get; set; }
			public string dataTransferRequired { get; set; }
			public string comptiaCode { get; set; }
			public string comptiaModifier { get; set; }
		}

		public class customerAddress {
			public string addressLine1 { get; set; }
			public string addressLine2 { get; set; }
			public string addressLine3 { get; set; }
			public string addressLine4 { get; set; }
			public string country { get; set; }
			public string zipCode { get; set; }
			public string regionCode { get; set; }
			public string county { get; set; }
			public string city { get; set; }
			public string state { get; set; }
			public string street { get; set; }
			public string firstName { get; set; }
			public string lastName { get; set; }
			public string middleInitial { get; set; }
			public string companyName { get; set; }
			public string primaryPhone { get; set; }
			public string secondaryPhone { get; set; }
			public string emailAddress { get; set; }
		}

		public class orderLines {
			public string partNumber { get; set; }
			public string comptiaCode { get; set; }
			public string comptiaModifier { get; set; }
			public string abused { get; set; }
			public string returnableDamage { get; set; }
			public string coveredByACPlus { get; set; }
			public string diagnosticCode { get; set; }
			public string consignmentFlag { get; set; }
			public string actualPartUsed { get; set; }
			public string replacementSerialNumber { get; set; }
			public string replacementIMEINumber { get; set; }
		}

		public class componentCheckDetails {
			public string component { get; set; }
			public string serialNumber { get; set; }
		}

		public class CreateCarryInResponse {
			public string operationId { get; set; }
			public repairConfirmation repairConfirmation { get; set; }
		}

		public class repairConfirmation {
			public string confirmationNumber { get; set; }
			public string diagnosticDescription { get; set; }
			public string diagnosticEventNumber { get; set; }
			public string diagnosticEventEndResult { get; set; }

			[XmlElementAttribute("parts", Form = System.Xml.Schema.XmlSchemaForm.Unqualified)]
			public parts[] parts { get; set; }

			public string totalFromOrder { get; set; }
			public string icmsTax { get; set; }
			public string pisTax { get; set; }
			public string ipiTax { get; set; }
			public string icmsStTax { get; set; }
			public string cofinsTax { get; set; }
			public string issTax { get; set; }
			public string vatTax { get; set; }
			public string outcome { get; set; }

			[XmlElementAttribute("messages", Form = System.Xml.Schema.XmlSchemaForm.Unqualified)]
			public string[] messages { get; set; }

			public availableRepairStrategies availableRepairStrategies { get; set; }
		}

		public class parts {
			public string availability { get; set; }
			public string currency { get; set; }
			public string netPrice { get; set; }
			public string partNumber { get; set; }
			public string partType { get; set; }
			public string quantity { get; set; }
		}

		public class availableRepairStrategies {
			public string availableRepairStrategy { get; set; }
		}
	}

	#endregion
}