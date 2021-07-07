using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace chat_service.Utilities
{
	public class RumbleController : ControllerBase
	{
		internal void Throw(string message, Exception exception = null)
		{
			throw new Exception(message, innerException: exception);
		}

		public override OkObjectResult Ok(object value)
		{
			return base.Ok(Merge(new {Success = true}, value));
		}

		private object Merge(object foo, object bar)
		{
			if (foo == null || bar == null)
				return foo ?? bar ?? new ExpandoObject();

			ExpandoObject expando = new ExpandoObject();
			IDictionary<string, object> result = (IDictionary<string, object>)expando;
			foreach (PropertyInfo fi in foo.GetType().GetProperties())
				result[fi.Name] = fi.GetValue(foo, null);
			foreach (PropertyInfo fi in bar.GetType().GetProperties())
				result[fi.Name] = fi.GetValue(bar, null);
			return result;
		}

		private string Uncapitalize(string s)
		{
			// TODO: Figure out why OK responses are capitalized
			return null;
		}
	}
}