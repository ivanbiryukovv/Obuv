using Obuv.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Obuv
{
    public partial class MainWindow : Window
    {
        private readonly User? _currentUser;
        List<ProductView> _products = new List<ProductView>();
        List<ProductView> _allProducts = new List<ProductView>();
        ProductWindow? _openProductWindow;

        private bool IsAdmin => _currentUser != null && (_currentUser.RoleId == 1 || string.Equals(_currentUser.Role?.Role1, "Администратор", StringComparison.OrdinalIgnoreCase));

        private bool IsManager => _currentUser != null && (_currentUser.RoleId == 2 || string.Equals(_currentUser.Role?.Role1, "Менеджер", StringComparison.OrdinalIgnoreCase));

        public MainWindow(User? user)
        {
            InitializeComponent();
            _currentUser = user;

            ConfigureUserAccess();
            SortBox.SelectedIndex = 0;
            LoadProviders();
            LoadProducts();
        }

        private void ConfigureUserAccess()
        {
            if (_currentUser == null)
            {
                UserNameBlock.Text = "Вы вошли как гость";
                MainGrid.RowDefinitions[1].Height = new GridLength(0);
                Filter.Visibility = Visibility.Collapsed;
                AddProductButton.Visibility = Visibility.Collapsed;
                OrdersButton.Visibility = Visibility.Collapsed;
                return;
            }

            UserNameBlock.Text = _currentUser.LastName + " " + _currentUser.FirstName + " " + _currentUser.Surname;

            if (IsAdmin || IsManager)
            {
                Filter.Visibility = Visibility.Visible;
                OrdersButton.Visibility = Visibility.Visible;
                AddProductButton.Visibility = IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                MainGrid.RowDefinitions[1].Height = new GridLength(0);
                Filter.Visibility = Visibility.Collapsed;
                AddProductButton.Visibility = Visibility.Collapsed;
                OrdersButton.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadProviders()
        {
            try
            {
                ObuvDbContext db = new ObuvDbContext();

                List<Provider> providers = new List<Provider>();
                providers.Add(new Provider { ProviderId = 0, Provider1 = "Все поставщики" });
                providers.AddRange(db.Providers.OrderBy(p => p.Provider1).ToList());

                ProvidersBox.ItemsSource = providers;
                ProvidersBox.DisplayMemberPath = "Provider1";
                ProvidersBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки поставщиков: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadProducts()
        {
            _allProducts.Clear();

            try
            {
                ObuvDbContext db = new ObuvDbContext();

                List<Product> products = db.Products.ToList();
                List<Manufacturer> manufacturers = db.Manufacturers.ToList();
                List<Provider> providers = db.Providers.ToList();
                List<ProductCategory> categories = db.ProductCategories.ToList();

                SolidColorBrush discountBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#2E8B57")!;
                Visibility deleteVisibility = IsAdmin ? Visibility.Visible : Visibility.Collapsed;
                Cursor cardCursor = IsAdmin ? Cursors.Hand : Cursors.Arrow;

                foreach (var product in products)
                {
                    var manufacturer = manufacturers.FirstOrDefault(m => m.ManufacturerId == product.ManufacturerId);
                    var provider = providers.FirstOrDefault(p => p.ProviderId == product.ProviderId);
                    var category = categories.FirstOrDefault(c => c.ProductCategoryId == product.ProductCategoryId);

                    string rawCategory = category?.ProductCategory1 ?? "Не указана";
                    string rawManufacturer = manufacturer?.Manufacturer1 ?? "Не указан";
                    string rawProvider = provider?.Provider1 ?? "Не указан";

                    decimal finalPrice = product.CurrentDiscount > 0 ? product.Price * (100 - product.CurrentDiscount) / 100m : product.Price;

                    Brush rowBackground = Brushes.White;
                    if (product.StockQuantity <= 0)
                    {
                        rowBackground = Brushes.LightBlue;
                    }
                    else if (product.CurrentDiscount > 15)
                    {
                        rowBackground = discountBrush;
                    }

                    ProductView view = new ProductView
                    {
                        ProductID = product.Article,
                        ProductName = product.ProductName,
                        RawCategory = rawCategory,
                        RawManufacturer = rawManufacturer,
                        RawProvider = rawProvider,
                        RawDescription = product.Description,
                        RawUnit = product.Unit,
                        CategoryAndName = rawCategory + " | " + product.ProductName,
                        DescriptionText = "Описание товара: " + product.Description,
                        ManufacturerName = "Производитель: " + rawManufacturer,
                        ProviderName = "Поставщик: " + rawProvider,
                        OriginalPrice = product.Price.ToString("C"),
                        FinalPrice = finalPrice.ToString("C"),
                        HasDiscountVisibility = product.CurrentDiscount > 0 ? Visibility.Visible : Visibility.Collapsed,
                        UnitText = "Единица измерения: " + product.Unit,
                        StockQuantityText = "Количество на складе: " + product.StockQuantity,
                        DiscountText = product.CurrentDiscount + "%",
                        StockQuantity = product.StockQuantity,
                        ProviderId = product.ProviderId,
                        Photo = ProductWindow.LoadPhoto(product.Photo),
                        RowBackground = rowBackground,
                        DeleteButtonVisibility = deleteVisibility,
                        CardCursor = cardCursor
                    };

                    _allProducts.Add(view);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки списка товаров: " + ex.Message, "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            _products.Clear();

            Provider? selectedProvider = ProvidersBox.SelectedItem as Provider;
            string searchText = SearchBox.Text.Trim();
            string[] searchWords = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var product in _allProducts)
            {
                if (selectedProvider != null && selectedProvider.ProviderId != 0 && product.ProviderId != selectedProvider.ProviderId)
                {
                    continue;
                }

                if (searchWords.Length > 0 && !MatchesAllWords(product, searchWords))
                {
                    continue;
                }

                _products.Add(product);
            }

            if (SortBox.SelectedIndex == 1)
            {
                _products = _products.OrderBy(p => p.StockQuantity).ToList();
            }
            else if (SortBox.SelectedIndex == 2)
            {
                _products = _products.OrderByDescending(p => p.StockQuantity).ToList();
            }

            ProductsList.ItemsSource = null;
            ProductsList.ItemsSource = _products;

            EmptyListBlock.Visibility = _products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool MatchesAllWords(ProductView product, string[] words)
        {
            foreach (string word in words)
            {
                bool matchesWord = ContainsIgnoreCase(product.ProductID, word) || ContainsIgnoreCase(product.ProductName, word) || ContainsIgnoreCase(product.RawCategory, word) || ContainsIgnoreCase(product.RawDescription, word) || ContainsIgnoreCase(product.RawManufacturer, word) || ContainsIgnoreCase(product.RawProvider, word) || ContainsIgnoreCase(product.RawUnit, word);

                if (!matchesWord)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ContainsIgnoreCase(string source, string value)
        {
            return !string.IsNullOrEmpty(source) && source.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        private void OpenProductEditor(string productArticle)
        {
            if (!IsAdmin)
            {
                MessageBox.Show("Добавление и редактирование товаров доступно только администратору.", "Доступ ограничен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_openProductWindow != null)
            {
                _openProductWindow.Activate();
                MessageBox.Show("Окно добавления или редактирования товара уже открыто. Закройте текущее окно перед открытием нового.", "Окно уже открыто", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProductWindow pw = string.IsNullOrEmpty(productArticle) ? new ProductWindow(LoadProducts) : new ProductWindow(productArticle, LoadProducts);
            pw.Owner = this;
            pw.Closed += (s, args) => { _openProductWindow = null; };
            _openProductWindow = pw;
            pw.Show();
        }

        private void ProductsList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source && FindParent<Button>(source) != null)
            {
                return;
            }

            if (sender is FrameworkElement fe && fe.DataContext is ProductView view)
            {
                OpenProductEditor(view.ProductID);
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T matched)
                {
                    return matched;
                }

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        private void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
            OpenProductEditor(string.Empty);
        }

        private void DeleteProductButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (!IsAdmin)
            {
                MessageBox.Show("Удаление товаров доступно только администратору.", "Доступ ограничен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_openProductWindow != null)
            {
                _openProductWindow.Activate();
                MessageBox.Show("Сначала закройте открытое окно редактирования товара.", "Окно уже открыто", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (sender is FrameworkElement fe && fe.DataContext is ProductView view)
            {
                if (ProductWindow.TryDeleteProduct(view.ProductID))
                {
                    LoadProducts();
                }
            }
        }

        private void OrdersButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null || (!IsAdmin && !IsManager))
            {
                MessageBox.Show("Просмотр заказов доступен только менеджеру и администратору.", "Доступ ограничен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_openProductWindow != null)
            {
                _openProductWindow.Activate();
                MessageBox.Show("Сначала закройте окно редактирования товара.", "Окно уже открыто", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OrdersWindow ordersWindow = new OrdersWindow(_currentUser);
            ordersWindow.Owner = this;
            ordersWindow.Closed += (s, args) =>
            {
                this.Show();
                LoadProducts();
            };

            this.Hide();
            ordersWindow.Show();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (_openProductWindow != null)
            {
                _openProductWindow.Close();
            }

            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private void ProvidersBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Clear();
            ProvidersBox.SelectedIndex = 0;
            SortBox.SelectedIndex = 0;

            ApplyFilters();
        }
    }

    public class ProductView
    {
        public string ProductID { get; set; }
        public string ProductName { get; set; }
        public string RawCategory { get; set; }
        public string RawManufacturer { get; set; }
        public string RawProvider { get; set; }
        public string RawDescription { get; set; }
        public string RawUnit { get; set; }
        public string CategoryAndName { get; set; }
        public string DescriptionText { get; set; }
        public string ManufacturerName { get; set; }
        public string ProviderName { get; set; }
        public string OriginalPrice { get; set; }
        public string FinalPrice { get; set; }
        public Visibility HasDiscountVisibility { get; set; }
        public string UnitText { get; set; }
        public string StockQuantityText { get; set; }
        public string DiscountText { get; set; }
        public int StockQuantity { get; set; }
        public int ProviderId { get; set; }
        public BitmapImage Photo { get; set; }
        public Brush RowBackground { get; set; }
        public Visibility DeleteButtonVisibility { get; set; }
        public Cursor CardCursor { get; set; }
    }
}
