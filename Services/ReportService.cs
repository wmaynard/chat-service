using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.CSharp.Common.Interop;
using Foo = System.Timers.Timer;

namespace Rumble.Platform.ChatService.Services
{
	public class ReportService : RumbleMongoService
	{
		// private const int SUMMARY_INTERVAL_MS = 3_600_000;
		private const int SUMMARY_INTERVAL_MS = 1000;
		private Timer SummaryTimer { get; set; }
		private new readonly IMongoCollection<Report> _collection;
		private readonly SlackMessageClient SlackReportChannel = new SlackMessageClient(
			channel: RumbleEnvironment.Variable("SLACK_REPORTS_CHANNEL"), 
			token: RumbleEnvironment.Variable("SLACK_CHAT_TOKEN"
		));

		public ReportService(ReportDBSettings settings) : base(settings)
		{
			Log.Write("Creating ReportService");
			_collection = _database.GetCollection<Report>(settings.CollectionName);
			SummaryTimer = new Timer(SUMMARY_INTERVAL_MS);
			SummaryTimer.Elapsed += SendSummaryReport;
			SummaryTimer.Start();
		}

		private void SendSummaryReport(object sender, ElapsedEventArgs args)
		{
			SummaryTimer.Stop();
			try
			{
				List<Report> reports = _collection.Find(r => r.Status == Report.STATUS_UNADDRESSED).ToList();
				ReportMetrics[] metrics = ReportMetrics.Generate(ref reports);
			}
			catch (Exception ex)
			{
				Log.Write(ex.Message);
			}

			SummaryTimer.Start();
			return;
		}

		public Report FindByPlayerAndMessage(string aid, string messageId)
		{
			return _collection.Find(filter: report => report.Reported.AccountId == aid && report.MessageId == messageId).FirstOrDefault();
		}
		
		public Report Get(string id)
		{
			Report output = _collection.Find(filter: r => r.Id == id).FirstOrDefault();
			if (output == null)
				throw new RumbleException("Report not found!"); // TODO: ReportNotFoundException
			return output;
		}
		public List<Report> List() => _collection.Find(filter: r => true).ToList();
		public void Create(Report report) => _collection.InsertOne(document: report);

		public void UpdateOrCreate(Report report)
		{
			if (report.Id == null)
				Create(report);
			else
				Update(report);
		}

		public void Update(Report report) =>
			_collection.ReplaceOne(filter: r => r.Id == report.Id, replacement: report);

		public void Remove(Report report) => _collection.DeleteOne(filter: r => r.Id == report.Id);

		public object SendToSlack(Report report)
		{
			bool success = false;
			try
			{
				SlackReportChannel.Send(report.SlackMessage);
				success = true;
			}
			catch { }
			return new { SlackSuccess = success };
		}
	}
}