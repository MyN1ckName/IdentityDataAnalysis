using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;

using System.Threading;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Structure;

namespace IdentityDataAnalysis.Windows.MainWindow
{
	class ViewModel : INotifyPropertyChanged
	{
		readonly UIDocument uiDoc;
		public ViewModel(UIDocument uiDoc)
		{
			this.uiDoc = uiDoc;
		}

		public string Title
		{
			get { return "IdentityDataAnalysis"; }
		}

		List<BuiltInParameter> builtInParameters = new List<BuiltInParameter>()
		{
			BuiltInParameter.ALL_MODEL_DESCRIPTION,
			BuiltInParameter.ALL_MODEL_MANUFACTURER,
			BuiltInParameter.ALL_MODEL_MODEL,
			BuiltInParameter.ALL_MODEL_TYPE_COMMENTS,
			BuiltInParameter.ALL_MODEL_URL,
		};
		public List<Parameter> Parameters
		{
			get {return GetParametersByBuiltInParameters(uiDoc, builtInParameters);}
		}

		Parameter selected;
		public Parameter Selected
		{
			get { return selected; }
			set
			{
				selected = value;
				OnPropertyChanged("Selected");
			}
		}

		private RelayCommand ok;
		public RelayCommand Ok
		{
			get
			{
				return ok ?? (ok = new RelayCommand(obj =>
				{
					Window window = obj as Window;
					window.Close();

					using (TransactionGroup tg = new TransactionGroup(uiDoc.Document, "IdentityDataAnalysis"))
					{
						tg.Start();
						CreateFilters(uiDoc, selected);
						SetViewStyle(uiDoc.Document);
						SetAnVisibilityFilter(uiDoc.Document);
						SetVisibilityFilter(uiDoc.Document);
						MessageShow(uiDoc);
						tg.Assimilate();
					}
				},
				obj =>
				{
					if (selected != null) { return true; }
					else { return false; }
				}));
			}
		}

		private RelayCommand cancel;
		public RelayCommand Cancel
		{
			get
			{
				return cancel ?? (cancel = new RelayCommand(obj =>
				{
					Window window = obj as Window;
					window.Close();
				}));
			}
		}
		// INotifyPropertyChanged interface implementation
		public event PropertyChangedEventHandler PropertyChanged;
		public void OnPropertyChanged([CallerMemberName] string prop = "")
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(prop));
		}

		private void CreateFilters(UIDocument uiDoc, Parameter parameter)
		{
			//TODO: Значение параметра не обязательно string!!!
			//StorageType p = uiDoc.Document.get_TypeOfStorage(parameter);
			IList<FilterRule> visibilityFilterRules = new List<FilterRule>();
			visibilityFilterRules.Add(ParameterFilterRuleFactory.CreateEqualsRule(parameter.Id, "", true));
			ElementParameterFilter visibilityFilter = new ElementParameterFilter(visibilityFilterRules);

			IList<FilterRule> anVisibilityFilterRules = new List<FilterRule>();
			anVisibilityFilterRules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(parameter.Id, "", true));
			ElementParameterFilter anVisibilityFilter = new ElementParameterFilter(anVisibilityFilterRules);

			List<ElementId> categorysIds = GetUniquCategorysIds(GetModelElementsByActiveView(uiDoc));
			ParameterFilterElement filterForVisibility = CreateFilterElement(uiDoc.Document, "IdentityDataAnalysis_Visibility", categorysIds, visibilityFilter);
			ParameterFilterElement filterForAnVisibility = CreateFilterElement(uiDoc.Document, "IdentityDataAnalysis_AnVisibility", categorysIds, anVisibilityFilter);
		}

		private List<ElementId> GetUniquCategorysIds(List<Element> elements)
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
					if (!view.GetFilters().Contains(filterForAnVisibility.Id))
					{
						view.AddFilter(filterForAnVisibility.Id);
					}
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
				ParameterFilterElement filterForVisibility = result.First() as ParameterFilterElement;
				View view = doc.ActiveView;


				using (Transaction t = new Transaction(doc, "SetVisibilityFilter"))
				{
					t.Start();
					if (!view.GetFilters().Contains(filterForVisibility.Id))
					{
						view.AddFilter(filterForVisibility.Id);
					}

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

					view.SetFilterOverrides(filterForVisibility.Id, settings);
					t.Commit();
				}
			}
			else
			{
				throw new NullReferenceException();
			}
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
					break;
				}
			}

			if (fillPatternElement != null)
			{
				return fillPatternElement;
			}
			else { throw new Exception(); }
		}

		private void MessageShow(UIDocument uiDoc)
		{
			string message = null;
			List<Element> elements = GetModelElementsByActiveView(uiDoc);
			foreach (ElementId categoryId in GetUniquCategorysIds(elements))
			{
				Category category = Category.GetCategory(uiDoc.Document, categoryId);
				message = String.Concat(message, category.Name + ":" + Environment.NewLine);
				foreach (ElementType elementType in GetUniquElementTypeByCategory(uiDoc.Document, category))
				{
					message = String.Concat(message, " -" + elementType.Name + Environment.NewLine);
				}
			}
			TaskDialog.Show("IdentityDataAnalysis", message);
		}

		private List<ElementType> GetUniquElementTypeByCategory(Document doc, Category category)
		{
			FilteredElementCollector collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
			collector.OfCategoryId(category.Id).ToElements();

			if (collector.Count() > 0)
			{
				List<ElementId> elementTypeIds = new List<ElementId>();
				foreach (Element element in collector)
				{
					elementTypeIds.Add(element.GetTypeId());
				}

				List<ElementType> elementTypes = new List<ElementType>();
				foreach (ElementId elementTypeId in elementTypeIds.Distinct().ToList<ElementId>())
				{
					elementTypes.Add(doc.GetElement(elementTypeId) as ElementType);
				}
				return elementTypes;
			}
			else { throw new ArgumentException(); }
		}

		private List<Parameter> GetParametersByBuiltInParameters(UIDocument uiDoc, List<BuiltInParameter> builtInParameters)
		{
			List<Element> elements = GetModelElementsByActiveView(uiDoc);
			List<Parameter> parameters = new List<Parameter>(builtInParameters.Count);
			foreach (BuiltInParameter builtInParameter in builtInParameters)
			{
				parameters.Add(uiDoc.Document.GetElement(elements.First().GetTypeId()).get_Parameter(builtInParameter));
			}
			return parameters;
		}
	}
}