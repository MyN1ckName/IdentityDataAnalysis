using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Attributes;

namespace IdentityDataAnalysis
{
	[TransactionAttribute(TransactionMode.Manual)]
	[RegenerationAttribute(RegenerationOption.Manual)]
	public class Command : IExternalCommand
	{
		public Result Execute(ExternalCommandData commandData,
			ref string messege,
			ElementSet elements)
		{
			try
			{
				if (commandData.Application.ActiveUIDocument.Document.ActiveView.IsTemporaryViewPropertiesModeEnabled())
				{
					throw new OperationCanceledException("Пожалуйста отключите режим временного переопределения графики");
				}
				System.Windows.Window window = new Windows.MainWindow.MainWindow()
				{
					DataContext = new Windows.MainWindow.ViewModel(commandData.Application.ActiveUIDocument)
				};
				window.ShowDialog();
				return Result.Succeeded;
			}

			catch (Autodesk.Revit.Exceptions.OperationCanceledException)
			{
				return Result.Cancelled;
			}
			catch (Exception ex)
			{
				messege = ex.Message;
				return Result.Failed;
			}
		}		
	}
}