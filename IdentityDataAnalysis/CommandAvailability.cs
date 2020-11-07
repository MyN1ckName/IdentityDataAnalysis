using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace IdentityDataAnalysis
{
	class CommandAvailability : IExternalCommandAvailability
	{
		public bool IsCommandAvailable(UIApplication applicationData
			, CategorySet selectedCategories)
		{
			if (applicationData.ActiveUIDocument.Document.ActiveView is ViewPlan ||
				applicationData.ActiveUIDocument.Document.ActiveView is View3D)
				return true;
			return false;
		}
	}
}