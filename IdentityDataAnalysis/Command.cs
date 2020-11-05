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
				using (TransactionGroup tg = new TransactionGroup(commandData.Application.ActiveUIDocument.Document, "IdentityDataAnalysis"))
				{
					tg.Start();

					CreateFilters(commandData.Application.ActiveUIDocument, BuiltInParameter.ALL_MODEL_MODEL);
					SetViewStyle(commandData.Application.ActiveUIDocument.Document);					
					SetAnVisibilityFilter(commandData.Application.ActiveUIDocument.Document);
					SetVisibilityFilter(commandData.Application.ActiveUIDocument.Document);

					//foreach(ElementId id in GetUniquCategoryIds(GetModelElementsByActiveView(commandData.Application.ActiveUIDocument)))
					//{
					//	TaskDialog.Show("pedik", id.ToString());
					//}


					tg.Assimilate();
				}
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

			ParameterFilterElement filterForVisibility = CreateFilterElement(uiDoc.Document, "IdentityDataAnalysis_Visibility", categorysIds, visibilityFilter);
			ParameterFilterElement filterForAnVisibility = CreateFilterElement(uiDoc.Document, "IdentityDataAnalysis_AnVisibility", categorysIds, anVisibilityFilter);
		}

		private ParameterFilterElement CreateFilterElement(Document doc, string name, List<ElementId> categoriesIds, ElementFilter elementFilter)
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

			if (filterElement != null)
			{
				return filterElement;
			}
			else { throw new Exception(); }
		}

		private void SetViewStyle(Document doc)
		{
			View view = doc.ActiveView;

			using (Transaction t = new Transaction(doc, "SetViewStyle"))
			{
				t.Start();
				view.EnableTemporaryViewPropertiesMode(view.Id);
				view.DetailLevel = ViewDetailLevel.Fine;
				view.get_Parameter(BuiltInParameter.MODEL_GRAPHICS_STYLE).Set(3);
				view.AreAnalyticalModelCategoriesHidden = true;
				t.Commit();
			}
		}

		private void SetAnVisibilityFilter(Document doc)
		{
			ElementClassFilter filter = new ElementClassFilter(typeof(ParameterFilterElement));
			FilteredElementCollector collector = new FilteredElementCollector(doc);
			collector.WherePasses(filter).ToElements()
				.Cast<ParameterFilterElement>().ToList<ParameterFilterElement>();

			var result =
				from item in collector
				where item.Name.Equals("IdentityDataAnalysis_AnVisibility")
				select item;

			if (result.Count() > 0)
			{
				ParameterFilterElement filterForAnVisibility = result.First() as ParameterFilterElement;
				View view = doc.ActiveView;

				using (Transaction t = new Transaction(doc, "SetAnVisibilityFilter"))
				{
					t.Start();
					view.AddFilter(filterForAnVisibility.Id);
					view.SetFilterVisibility(filterForAnVisibility.Id, false);
					t.Commit();
				}
			}
			else { throw new NullReferenceException(); }
		}

		private void SetVisibilityFilter(Document doc)
		{
			ElementClassFilter filter = new ElementClassFilter(typeof(ParameterFilterElement));
			FilteredElementCollector collector = new FilteredElementCollector(doc);
			collector.WherePasses(filter).ToElements()
				.Cast<ParameterFilterElement>().ToList<ParameterFilterElement>();

			var result =
				from item in collector
				where item.Name.Equals("IdentityDataAnalysis_Visibility")
				select item;

			if (result.Count() > 0)
			{
				ParameterFilterElement filterForAnVisibility = result.First() as ParameterFilterElement;
				View view = doc.ActiveView;

				using (Transaction t = new Transaction(doc, "SetVisibilityFilter"))
				{
					t.Start();
					view.AddFilter(filterForAnVisibility.Id);

					OverrideGraphicSettings settings = new OverrideGraphicSettings();

					FillPatternElement fillPatternElement = GetSolidFillPaeern(doc);
					settings.SetSurfaceForegroundPatternId(fillPatternElement.Id);
					settings.SetSurfaceBackgroundPatternId(fillPatternElement.Id);
					settings.SetCutForegroundPatternId(fillPatternElement.Id);
					settings.SetCutBackgroundPatternId(fillPatternElement.Id);

					Color redColor = new Color(255, 0, 0);
					settings.SetSurfaceForegroundPatternColor(redColor);
					settings.SetSurfaceBackgroundPatternColor(redColor);
					settings.SetCutForegroundPatternColor(redColor);
					settings.SetCutBackgroundPatternColor(redColor);

					settings.SetSurfaceTransparency(20);

					view.SetFilterOverrides(filterForAnVisibility.Id, settings);
					t.Commit();
				}
			}
			else { throw new NullReferenceException(); }

		}

		private FillPatternElement GetSolidFillPaeern(Document doc)
		{
			ElementClassFilter filter = new ElementClassFilter(typeof(FillPatternElement));
			FilteredElementCollector collector = new FilteredElementCollector(doc);
			collector.WherePasses(filter).ToElements()
				.Cast<FillPatternElement>().ToList<FillPatternElement>();

			FillPatternElement fillPatternElement = null;
			foreach (FillPatternElement fpe in collector)
			{
				if (fpe.GetFillPattern().IsSolidFill)
				{
					fillPatternElement = fpe;
				}
			}

			if (fillPatternElement != null)
			{
				return fillPatternElement;
			}
			else { throw new Exception(); }
		}
	}
}