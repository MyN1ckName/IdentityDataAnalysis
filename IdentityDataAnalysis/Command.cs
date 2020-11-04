using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
				List<Element> modelElements = GetModelElementsByActiveView(commandData.Application.ActiveUIDocument);
				//SelectElement(commandData.Application.ActiveUIDocument);

				//GetUniquTypeIds(modelElements);

				//GetUniquCategorys(modelElements);
				//TaskDialog.Show("pedik", GetUniquCategoryIds(modelElements).Count.ToString());

				//TestCreateViewFilter(commandData.Application.ActiveUIDocument.Document, commandData.Application.ActiveUIDocument.Document.ActiveView);

				CreateFilters(commandData.Application.ActiveUIDocument, BuiltInParameter.ALL_MODEL_MODEL);


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

		private List<Element> GetModelElementsByActiveView(UIDocument uiDoc)
		{
			// See Model Elements
			// https://help.autodesk.com/view/RVT/2020/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Elements_Essentials_Element_Classification_html

			FilteredElementCollector familyInstanceCollector = new FilteredElementCollector(uiDoc.Document, uiDoc.Document.ActiveView.Id);
			familyInstanceCollector.OfClass(typeof(FamilyInstance));

			FilteredElementCollector hostObjectCollector = new FilteredElementCollector(uiDoc.Document, uiDoc.Document.ActiveView.Id);
			hostObjectCollector.OfClass(typeof(HostObject));

			IEnumerable<Element> modelElements = familyInstanceCollector.ToElements().Union(hostObjectCollector.ToElements());

			if (modelElements.Count() > 0)
			{
				return modelElements.ToList<Element>();
			}
			else { throw new NullReferenceException("The View does not contains Model Elements"); }
		}

		private List<ElementId> GetUniquCategoryIds(List<Element> elements)
		{
			if (elements.Count > 0)
			{
				List<ElementId> elementCategory = new List<ElementId>();
				foreach (Element element in elements)
				{
					elementCategory.Add(element.Category.Id);
				}

				return elementCategory.Distinct().ToList<ElementId>();
			}
			else { throw new ArgumentException(); }
		}

		// TODO: test
		private void TestCreateViewFilter(Document doc, View view)
		{
			List<ElementId> categories = new List<ElementId>();
			categories.Add(new ElementId(BuiltInCategory.OST_Walls));

			using (Transaction t = new Transaction(doc, "TestCreateViewFilter"))
			{
				t.Start();
				ParameterFilterElement testFilter = ParameterFilterElement.Create(doc, "TestFilter", categories);
				view.AddFilter(testFilter.Id);
				t.Commit();
			}
		}

		private void CreateFilters(UIDocument uiDoc, BuiltInParameter parameter)
		{
			//TODO: Значение параметра не обязательно string!!!
			//StorageType p = uiDoc.Document.get_TypeOfStorage(parameter);


			IList<FilterRule> visibilityFilterRules = new List<FilterRule>();
			visibilityFilterRules.Add(ParameterFilterRuleFactory.CreateEqualsRule(new ElementId(parameter), "", true));
			ElementParameterFilter visibilityFilter = new ElementParameterFilter(visibilityFilterRules);

			IList<FilterRule> anVisibilityFilterRules = new List<FilterRule>();
			anVisibilityFilterRules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(new ElementId(parameter), "", true));
			ElementParameterFilter anVisibilityFilter = new ElementParameterFilter(anVisibilityFilterRules);

			List<ElementId> categorysIds = GetUniquCategoryIds(GetModelElementsByActiveView(uiDoc));

			CreateFilterElement(uiDoc.Document, "IdentityDataAnalysis_Visibility", categorysIds, visibilityFilter);
			CreateFilterElement(uiDoc.Document, "IdentityDataAnalysis_AnVisibility", categorysIds, anVisibilityFilter);
		}

		private void CreateFilterElement(Document doc, string name, List<ElementId> categoriesIds, ElementFilter elementFilter)
		{
			ElementClassFilter filter = new ElementClassFilter(typeof(ParameterFilterElement));
			FilteredElementCollector collector = new FilteredElementCollector(doc);
			collector.WherePasses(filter).ToElements()
				.Cast<ParameterFilterElement>().ToList<ParameterFilterElement>();

			var result =
				from item in collector
				where item.Name.Equals(name)
				select item;

			ParameterFilterElement filterElement;
			if (result.Count() == 0)
			{
				using (Transaction t = new Transaction(doc, "CreateFilterElement"))
				{
					t.Start();
					filterElement = ParameterFilterElement.Create(doc, name, categoriesIds, elementFilter);

					t.Commit();
				}
			}
			else
			{
				filterElement = result.First() as ParameterFilterElement;

				using (Transaction t = new Transaction(doc, "SetCategoriesFilterElement"))
				{
					t.Start();
					filterElement.SetCategories(categoriesIds);
					filterElement.SetElementFilter(elementFilter);
					t.Commit();
				}
			}
		}

		//public static void CreateViewFilter(Document doc, View view)
		//{
		//	List<ElementId> categories = new List<ElementId>();
		//	categories.Add(new ElementId(BuiltInCategory.OST_Walls));
		//	List<FilterRule> filterRules = new List<FilterRule>();
		//
		//	using (Transaction t = new Transaction(doc, "Add view filter"))
		//	{
		//		t.Start();
		//
		//		// Create filter element assocated to the input categories
		//		ParameterFilterElement parameterFilterElement = ParameterFilterElement.Create(doc, "Example view filter", categories);
		//
		//		// Criterion 1 - wall type Function is "Exterior"
		//		ElementId exteriorParamId = new ElementId(BuiltInParameter.FUNCTION_PARAM);
		//		filterRules.Add(ParameterFilterRuleFactory.CreateEqualsRule(exteriorParamId, (int)WallFunction.Exterior));
		//
		//		// Criterion 2 - wall height > some number
		//		ElementId lengthId = new ElementId(BuiltInParameter.CURVE_ELEM_LENGTH);
		//		filterRules.Add(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(lengthId, 28.0, 0.0001));
		//
		//		// Criterion 3 - custom shared parameter value matches string pattern
		//		// Get the id for the shared parameter - the ElementId is not hardcoded, so we need to get an instance of this type to find it
		//		Guid spGuid = new Guid("96b00b61-7f5a-4f36-a828-5cd07890a02a");
		//		FilteredElementCollector collector = new FilteredElementCollector(doc);
		//		collector.OfClass(typeof(Wall));
		//		Wall wall = collector.FirstElement() as Wall;
		//
		//		if (wall != null)
		//		{
		//			Parameter sharedParam = wall.get_Parameter(spGuid);
		//			ElementId sharedParamId = sharedParam.Id;
		//
		//			filterRules.Add(ParameterFilterRuleFactory.CreateBeginsWithRule(sharedParamId, "15.", true));
		//		}
		//
		//		ElementFilter elemFilter = CreateElementFilterFromFilterRules(filterRules);
		//		parameterFilterElement.SetElementFilter(elemFilter);
		//
		//		// Apply filter to view
		//		view.AddFilter(parameterFilterElement.Id);
		//		view.SetFilterVisibility(parameterFilterElement.Id, false);
		//		t.Commit();
		//	}
		//}

		//private List<ElementId> GetUniquTypeIds(List<Element> elements)
		//{
		//	if (elements.Count > 0)
		//	{
		//		List<ElementId> elementTypeIds = new List<ElementId>();
		//		foreach (Element element in elements)
		//		{
		//			elementTypeIds.Add(element.GetTypeId());
		//		}
		//		return elementTypeIds.Distinct().ToList<ElementId>();
		//	}
		//	else { throw new ArgumentException(); }
		//}

		// TODO: test
		private void SelectElement(UIDocument uiDoc)
		{
			// See Model Elements
			// https://help.autodesk.com/view/RVT/2020/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Elements_Essentials_Element_Classification_html

			FilteredElementCollector familyInstanceCollector = new FilteredElementCollector(uiDoc.Document, uiDoc.Document.ActiveView.Id);
			familyInstanceCollector.OfClass(typeof(FamilyInstance));

			FilteredElementCollector hostObjectCollector = new FilteredElementCollector(uiDoc.Document, uiDoc.Document.ActiveView.Id);
			hostObjectCollector.OfClass(typeof(HostObject));

			IEnumerable<ElementId> modelElements = familyInstanceCollector.ToElementIds().Union(hostObjectCollector.ToElementIds());

			uiDoc.Selection.SetElementIds(modelElements.ToList<ElementId>());
		}
	}
}