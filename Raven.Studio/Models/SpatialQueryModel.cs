using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Raven.Abstractions.Indexing;
using Raven.Studio.Commands;
using Raven.Studio.Infrastructure;

namespace Raven.Studio.Models
{
	public class SpatialQueryField
	{
		public string Name { get; set; }
		public bool Geographical { get; set; }
	}

	public class SpatialQueryModel : NotifyPropertyChangedBase
	{
		protected PerDatabaseState PerDatabaseState
		{
			get { return ApplicationModel.Current.State.Databases[ApplicationModel.Database.Value]; }
		}

		public SpatialQueryModel()
		{
			Fields = new BindableCollection<string>(x => x);
		}

		public string IndexName { get; set; }
		private readonly Dictionary<string, bool> fields = new Dictionary<string, bool>();
		public BindableCollection<string> Fields { get; private set; }

		public void UpdateFields(IEnumerable<SpatialQueryField> queryFields)
		{
			fields.Clear();
			Fields.Clear();
			
			foreach (var queryField in queryFields.OrderBy(x => x.Name))
			{
				Fields.Add(queryField.Name);
				fields[queryField.Name] = queryField.Geographical;
			}

			FieldName = Fields.FirstOrDefault();
		}

		public void Clear()
		{
			Y = null;
			X = null;
			Radius = null;
		}

		public SpatialUnits RadiusUnits { get; set; }

		private string fieldName;
		public string FieldName
		{
			get { return fieldName; }
			set
			{
				if (fieldName == value) return;
				fieldName = value;
				IsGeographical = fields[fieldName];
				OnPropertyChanged(() => FieldName);
			}
		}

		private bool isGeographical;
		public bool IsGeographical
		{
			get { return isGeographical; }
			set
			{
				if (isGeographical == value) return;
				isGeographical = value;
				OnPropertyChanged(() => IsGeographical);
			}
		}

		private double? x;
		public double? X
		{
			get { return x; }
			set
			{
				if (x == value) return;
				x = value;
				address = null;
				OnPropertyChanged(() => Address);
				OnPropertyChanged(() => X);
			}
		}

		private double? y;
		public double? Y
		{
			get { return y; }
			set
			{
				if (y == value) return;
				y = value;
				address = null;
				OnPropertyChanged(() => Address);
				OnPropertyChanged(() => Y);
			}
		}

		private double? radius;
		public double? Radius
		{
			get { return radius; }
			set
			{
				if (radius == value) return;
				radius = value;
				OnPropertyChanged(() => Radius);
			}
		}

		private string address;
		public string Address
		{
			get { return address; }
			set
			{
				UpdateAddress(value);
				OnPropertyChanged(() => X);
				OnPropertyChanged(() => Y);
				OnPropertyChanged(() => Address);
			}
		}

		private void UpdateAddress(string value)
		{
			if (PerDatabaseState.RecentAddresses.ContainsKey(IndexName) && PerDatabaseState.RecentAddresses[IndexName].ContainsKey(value))
			{
				var data = PerDatabaseState.RecentAddresses[IndexName][value];
				if (data != null)
				{
					address = data.Address;
					y = data.Latitude;
					x = data.Longitude;
					return;
				}
			}

			address = value;
			y = null;
			x = null;
		}

		public void UpdateResultsFromCalculate(AddressData addressData)
		{
			y = addressData.Latitude;
			x = addressData.Longitude;

			var addresses = PerDatabaseState.RecentAddresses.ContainsKey(IndexName) ? PerDatabaseState.RecentAddresses[IndexName] : new Dictionary<string, AddressData>();

			addresses[addressData.Address] = addressData;

			PerDatabaseState.RecentAddresses[IndexName] = addresses;

			OnPropertyChanged(() => Y);
			OnPropertyChanged(() => X);
		}

		public ICommand CalculateFromAddress { get { return new CalculateGeocodeFromAddressCommand(this); } }
	}
}