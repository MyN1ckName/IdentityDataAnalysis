using System;
using Autodesk.Revit.UI;

namespace IdentityDataAnalysis
{
	class App : IExternalApplication
	{
		static readonly string ExecutingAssemblyPath = System.Reflection.Assembly
		.GetExecutingAssembly().Location;

		public Result OnStartup(UIControlledApplication app)
		{
			CreatePanel(app);
			return Result.Succeeded;
		}
		public Result OnShutdown(UIControlledApplication app)
		{
			return Result.Succeeded;
		}
		void CreatePanel(UIControlledApplication app)
		{
			PushButtonData data = new PushButtonData(
				"IdentityDataAnalysis",
				"IdentityDataAnalysis",
				ExecutingAssemblyPath,
				"IdentityDataAnalysis.Command");
			data.LargeImage = new System.Windows.Media.Imaging.BitmapImage
				(new Uri("pack://application:,,,/IdentityDataAnalysis;component/img/icon32.png", UriKind.Absolute));
			data.AvailabilityClassName = "IdentityDataAnalysis.CommandAvailability";

			RibbonPanel projectPanel = app.CreateRibbonPanel("IdentityDataAnalysis");
			PushButton exp = projectPanel.AddItem(data) as PushButton;
		}
	}
}