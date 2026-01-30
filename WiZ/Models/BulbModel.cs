using System;
using System.ComponentModel;
using System.Drawing;
using System.Net;
using WiZ.Observable;
using WiZ.Profiles;

using System.Threading.Tasks;

namespace WiZ.Models
{
    /// <summary>
    /// Represents a WiZ bulb with its state and properties.
    /// This is a pure model class that holds bulb data and state information.
    /// </summary>
    public class BulbModel : ObservableBase, IBulb
    {
        private string _name;
        private string _bulbType;
        private string _icon;
        private IPAddress _ipAddress;
        private int _port;
        private MACAddress _macAddress;
        private BulbParams _settings;
        private DateTime _lastSeen;

        /// <summary>
        /// Gets or sets the bulb name.
        /// </summary>
        public string Name
        {
            get => _name ?? string.Empty;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// Gets or sets the bulb type/model.
        /// </summary>
        public string BulbType
        {
            get => _bulbType ?? string.Empty;
            set => SetProperty(ref _bulbType, value);
        }

        /// <summary>
        /// Gets or sets the bulb icon.
        /// </summary>
        public string Icon
        {
            get => _icon ?? string.Empty;
            set => SetProperty(ref _icon, value);
        }

        /// <summary>
        /// Gets or sets the IP address of the bulb.
        /// </summary>
        public IPAddress IPAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        /// <summary>
        /// Gets or sets the port number of the bulb.
        /// </summary>
        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        /// <summary>
        /// Gets or sets the MAC address of the bulb.
        /// </summary>
        public MACAddress MACAddress
        {
            get => _macAddress;
            set => SetProperty(ref _macAddress, value);
        }

        /// <summary>
        /// Gets or sets the bulb settings and current state.
        /// </summary>
        public BulbParams Settings
        {
            get => _settings ?? new BulbParams();
            set
            {
                if (_settings != null)
                    _settings.PropertyChanged -= SettingsChanged;
                
                SetProperty(ref _settings, value);
                
                if (_settings != null)
                    _settings.PropertyChanged += SettingsChanged;
            }
        }

        /// <summary>
        /// Gets or sets when the bulb was last seen/communicated with.
        /// </summary>
        public DateTime LastSeen
        {
            get => _lastSeen;
            set => SetProperty(ref _lastSeen, value);
        }

        /// <summary>
        /// Gets whether the bulb is powered on.
        /// </summary>
        public bool IsPoweredOn => Settings.State ?? false;

        /// <summary>
        /// Gets the current brightness level (0-100).
        /// </summary>
        public int Brightness => Settings.Brightness ?? 0;

        /// <summary>
        /// Gets the current light mode.
        /// </summary>
        public LightMode LightMode => Settings.LightModeInfo ?? LightMode.Custom;

        /// <summary>
        /// Gets the current scene ID.
        /// </summary>
        public int Scene => Settings.Scene ?? 1;

        /// <summary>
        /// Gets the current color temperature in Kelvin.
        /// </summary>
        public int Temperature => Settings.Temperature ?? 0;

        /// <summary>
        /// Gets the current RGB color.
        /// </summary>
        public Color Color => Settings.Color ?? Color.Empty;

        /// <summary>
        /// Gets the connection status based on last seen time.
        /// </summary>
        public bool IsOnline => DateTime.Now.Subtract(LastSeen).TotalMinutes < 5;

        /// <summary>
        /// Gets or sets the home ID.
        /// </summary>
        public int? HomeId
        {
            get => Settings.HomeId;
            set => Settings.HomeId = value;
        }

        /// <summary>
        /// Gets or sets the room ID.
        /// </summary>
        public int? RoomId
        {
            get => Settings.RoomId;
            set => Settings.RoomId = value;
        }

        #region Public Constructors

        /// <summary>
        /// Creates a new BulbModel instance.
        /// </summary>
        public BulbModel()
        {
            _settings = new BulbParams();
            _settings.PropertyChanged += SettingsChanged;
            LastSeen = DateTime.Now;
            Port = 38899; // Bulb.DefaultPort
        }

        /// <summary>
        /// Creates a new BulbModel with specified IP address.
        /// </summary>
        /// <param name="ipAddress">IP address of the bulb.</param>
        public BulbModel(IPAddress ipAddress) : this()
        {
            IPAddress = ipAddress;
            Port = 38899;
        }

        /// <summary>
        /// Creates a new BulbModel with specified IP address and port.
        /// </summary>
        /// <param name="ipAddress">IP address of the bulb.</param>
        /// <param name="port">Port number of the bulb.</param>
        public BulbModel(IPAddress ipAddress, int port) : this()
        {
            IPAddress = ipAddress;
            Port = port;
        }

        /// <summary>
        /// Creates a new BulbModel with specified IP address.
        /// </summary>
        /// <param name="ipAddress">IP address as string.</param>
        public BulbModel(string ipAddress) : this(IPAddress.Parse(ipAddress))
        {
        }

        /// <summary>
        /// Creates a new BulbModel with specified IP address and port.
        /// </summary>
        /// <param name="ipAddress">IP address as string.</param>
        /// <param name="port">Port number.</param>
        public BulbModel(string ipAddress, int port) : this(IPAddress.Parse(ipAddress), port)
        {
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles property changes from settings to bubble up notifications.
        /// </summary>
        private void SettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            // Bubble up important property changes
            switch (e.PropertyName)
            {
                case nameof(BulbParams.Brightness):
                    OnPropertyChanged(nameof(Brightness));
                    break;
                case nameof(BulbParams.State):
                    OnPropertyChanged(nameof(IsPoweredOn));
                    break;
                case nameof(BulbParams.LightModeInfo):
                    OnPropertyChanged(nameof(LightMode));
                    break;
                case nameof(BulbParams.Scene):
                    OnPropertyChanged(nameof(Scene));
                    break;
                case nameof(BulbParams.Temperature):
                    OnPropertyChanged(nameof(Temperature));
                    break;
                case nameof(BulbParams.Color):
                    OnPropertyChanged(nameof(Color));
                    break;
                case nameof(BulbParams.MACAddress):
                    OnPropertyChanged(nameof(MACAddress));
                    break;
                case nameof(BulbParams.HomeId):
                    OnPropertyChanged(nameof(HomeId));
                    break;
                case nameof(BulbParams.RoomId):
                    OnPropertyChanged(nameof(RoomId));
                    break;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the last seen timestamp to current time.
        /// </summary>
        public void UpdateLastSeen()
        {
            LastSeen = DateTime.Now;
        }

        /// <summary>
        /// Creates a copy of this bulb model.
        /// </summary>
        /// <returns>A new BulbModel with the same properties.</returns>
        public BulbModel Clone()
        {
            return new BulbModel(IPAddress, Port)
            {
                Name = Name,
                BulbType = BulbType,
                Icon = Icon,
                MACAddress = MACAddress,
                Settings = (BulbParams)(Settings?.Clone() ?? new BulbParams()),
                LastSeen = LastSeen
            };
        }

        public Task<BulbModel> GetBulbModel()
        {
            return Task.FromResult(this);
        }

        #endregion
    }
}