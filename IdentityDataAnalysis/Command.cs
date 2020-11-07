using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
				TaskDialog.Show("Ошибка", ex.Message);
				return Result.Failed;
			}
		}		
	}
}