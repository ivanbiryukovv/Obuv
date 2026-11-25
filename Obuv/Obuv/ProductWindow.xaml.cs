using Microsoft.Win32;
using Obuv.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Obuv
{
    public partial class ProductWindow : Window
    {
        private static readonly string[] ProtectedImages = { "picture.png", "Icon.png", "Icon.ico", "Icon.JPG" };
        private readonly string? _productID;
        private readonly Action? _onSaved;
        private string? _selectedPhotoSourcePath;
        private bool _photoCleared;

        private bool IsEditMode => !string.IsNullOrWhiteSpace(_productID);

        public ProductWindow(string productId) : this(productId, null)
        {
        }

        public ProductWindow(Action? onSaved)
        {
            _onSaved = onSaved;
            InitializeComponent();
            InitializeForm();
        }

        public ProductWindow(string productId, Action? onSaved)
        {
            _productID = productId;
            _onSaved = onSaved;
            InitializeComponent();
            InitializeForm();
        }

        private void InitializeForm()
        {
            LoadDictionaries();

            if (IsEditMode)
            {
                Title = "ОБУВЬ - Редактирование товара";
                HeaderTitleBlock.Text = "Редактирование товара";
                IdPanel.Visibility = Visibility.Visible;
                DeleteButton.Visibility = Visibility.Visible;
                LoadProduct();
            }
            else
            {
                Title = "ОБУВЬ - Добавление товара";
                HeaderTitleBlock.Text = "Добавление товара";
                IdPanel.Visibility = Visibility.Collapsed;
                DeleteButton.Visibility = Visibility.Collapsed;
                UnitBox.Text = "шт.";
                StockBox.Text = "0";
                DiscountBox.Text = "0";
                ProductImage.Source = LoadPhoto(null);
            }
        }

        private void LoadDictionaries()
        {
            try
            {
                ObuvDbContext db = new ObuvDbContext();

                List<ProductCategory> categories = db.ProductCategories.OrderBy(c => c.ProductCategory1).ToList();
                List<Manufacturer> manufacturers = db.Manufacturers.OrderBy(m => m.ManufacturerId).ToList().GroupBy(m => m.Manufacturer1).Select(g => g.First()).OrderBy(m => m.Manufacturer1).ToList();
                List<Provider> providers = db.Providers.OrderBy(p => p.Provider1).ToList();

                CategoryBox.ItemsSource = categories;
                CategoryBox.DisplayMemberPath = "ProductCategory1";
                CategoryBox.SelectedValuePath = "ProductCategoryId";

                ManufacturerBox.ItemsSource = manufacturers;
                ManufacturerBox.DisplayMemberPath = "Manufacturer1";
                ManufacturerBox.SelectedValuePath = "ManufacturerId";

                ProviderBox.ItemsSource = providers;
                ProviderBox.DisplayMemberPath = "Provider1";
                ProviderBox.SelectedValuePath = "ProviderId";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить справочники: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProduct()
        {
            try
            {
                ObuvDbContext db = new ObuvDbContext();
                Product? product = db.Products.FirstOrDefault(p => p.Article == _productID);
                if (product == null)
                {
                    MessageBox.Show("Выбранный товар не найден в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }

                ArticleBox.Text = product.Article;
                NameBox.Text = product.ProductName;
                CategoryBox.SelectedValue = product.ProductCategoryId;
                ManufacturerBox.SelectedValue = product.ManufacturerId;

                if (ManufacturerBox.SelectedItem == null)
                {
                    string? mfgName = db.Manufacturers.Where(m => m.ManufacturerId == product.ManufacturerId).Select(m => m.Manufacturer1).FirstOrDefault();
                    if (!string.IsNullOrEmpty(mfgName) && ManufacturerBox.ItemsSource is IEnumerable<Manufacturer> list)
                    {
                        ManufacturerBox.SelectedItem = list.FirstOrDefault(m => m.Manufacturer1 == mfgName);
                    }
                }

                ProviderBox.SelectedValue = product.ProviderId;
                PriceBox.Text = product.Price.ToString("0.##", CultureInfo.InvariantCulture);
                UnitBox.Text = product.Unit;
                StockBox.Text = product.StockQuantity.ToString();
                DiscountBox.Text = product.CurrentDiscount.ToString();
                DescriptionBox.Text = product.Description;
                ProductImage.Source = LoadPhoto(product.Photo);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных товара: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Выберите изображение товара";
            dialog.Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ProductImage.Source = LoadFromFileBytes(dialog.FileName);
                _selectedPhotoSourcePath = dialog.FileName;
                _photoCleared = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть выбранный файл изображения: " + ex.Message, "Ошибка изображения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedPhotoSourcePath = null;
            _photoCleared = true;
            ProductImage.Source = LoadPhoto(null);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out decimal price, out int stockQuantity, out int discount))
            {
                return;
            }

            ProductCategory selectedCategory = (ProductCategory)CategoryBox.SelectedItem;
            Manufacturer selectedManufacturer = (Manufacturer)ManufacturerBox.SelectedItem;
            Provider selectedProvider = (Provider)ProviderBox.SelectedItem;

            try
            {
                ObuvDbContext db = new ObuvDbContext();
                Product product;
                string? oldPhoto = null;

                if (IsEditMode)
                {
                    Product? existing = db.Products.FirstOrDefault(p => p.Article == _productID);
                    if (existing == null)
                    {
                        MessageBox.Show("Редактируемый товар больше не существует в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    product = existing;
                    oldPhoto = existing.Photo;
                }
                else
                {
                    product = new Product { Article = GenerateNextArticle(db) };
                    db.Products.Add(product);
                }

                product.ProductName = NameBox.Text.Trim();
                product.ProductCategoryId = selectedCategory.ProductCategoryId;
                product.ManufacturerId = selectedManufacturer.ManufacturerId;
                product.ProviderId = selectedProvider.ProviderId;
                product.Price = price;
                product.Unit = UnitBox.Text.Trim();
                product.StockQuantity = stockQuantity;
                product.CurrentDiscount = discount;
                product.Description = DescriptionBox.Text.Trim();

                if (!string.IsNullOrEmpty(_selectedPhotoSourcePath))
                {
                    product.Photo = SaveResizedProductPhoto(_selectedPhotoSourcePath, product.Article);
                }
                else if (_photoCleared)
                {
                    product.Photo = null;
                }

                db.SaveChanges();

                if (IsEditMode && (!string.IsNullOrEmpty(_selectedPhotoSourcePath) || _photoCleared))
                {
                    DeleteProductPhotoIfUnused(db, oldPhoto, product.Article);
                }

                _onSaved?.Invoke();
                MessageBox.Show(IsEditMode ? "Изменения товара успешно сохранены." : "Новый товар успешно добавлен.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения товара в базу данных: " + ex.Message, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateInputs(out decimal price, out int stockQuantity, out int discount)
        {
            price = 0;
            stockQuantity = 0;
            discount = 0;

            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("Введите наименование товара.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                NameBox.Focus();
                return false;
            }

            if (CategoryBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите категорию товара из выпадающего списка.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                CategoryBox.Focus();
                return false;
            }

            if (ManufacturerBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите производителя из выпадающего списка.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                ManufacturerBox.Focus();
                return false;
            }

            if (ProviderBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите поставщика из выпадающего списка.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                ProviderBox.Focus();
                return false;
            }

            string normalizedPrice = PriceBox.Text.Trim().Replace(" ", "").Replace("\u00A0", "").Replace(',', '.');
            if (!decimal.TryParse(normalizedPrice, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out price) || price < 0 || Math.Round(price, 2) != price)
            {
                MessageBox.Show("Введите корректную стоимость товара. Цена не может быть отрицательной и может содержать не более двух знаков после запятой.", "Ошибка ввода цены", MessageBoxButton.OK, MessageBoxImage.Error);
                PriceBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(UnitBox.Text))
            {
                MessageBox.Show("Укажите единицу измерения товара (например, шт.).", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                UnitBox.Focus();
                return false;
            }

            if (!int.TryParse(StockBox.Text.Trim(), out stockQuantity) || stockQuantity < 0)
            {
                MessageBox.Show("Количество товара на складе должно быть целым неотрицательным числом.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                StockBox.Focus();
                return false;
            }

            if (!int.TryParse(DiscountBox.Text.Trim(), out discount) || discount < 0 || discount > 100)
            {
                MessageBox.Show("Действующая скидка должна быть целым числом от 0 до 100.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                DiscountBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(DescriptionBox.Text))
            {
                MessageBox.Show("Введите описание товара.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                DescriptionBox.Focus();
                return false;
            }

            return true;
        }

        private string GenerateNextArticle(ObuvDbContext db)
        {
            int maxNumber = 0;
            foreach (string article in db.Products.Select(p => p.Article).ToList())
            {
                if (int.TryParse(article, out int number) && number > maxNumber)
                {
                    maxNumber = number;
                }
            }

            int nextNumber = maxNumber > 0 ? maxNumber + 1 : db.Products.Count() + 1;
            while (db.Products.Any(p => p.Article == nextNumber.ToString()))
            {
                nextNumber++;
            }

            return nextNumber.ToString();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsEditMode || string.IsNullOrEmpty(_productID))
            {
                return;
            }

            if (TryDeleteProduct(_productID))
            {
                _onSaved?.Invoke();
                this.Close();
            }
        }

        public static bool TryDeleteProduct(string article)
        {
            try
            {
                ObuvDbContext db = new ObuvDbContext();
                Product? product = db.Products.FirstOrDefault(p => p.Article == article);
                if (product == null)
                {
                    MessageBox.Show("Выбранный товар не найден в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (db.OrderProducts.Any(op => op.Article == article))
                {
                    MessageBox.Show("Товар \"" + product.ProductName + "\" (артикул " + article + ") присутствует в заказе и не может быть удалён.", "Удаление невозможно", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                MessageBoxResult confirm = MessageBox.Show("Вы действительно хотите безвозвратно удалить товар \"" + product.ProductName + "\" (артикул " + article + ")?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                {
                    return false;
                }

                string? oldPhoto = product.Photo;
                db.Products.Remove(product);
                db.SaveChanges();

                DeleteProductPhotoIfUnused(db, oldPhoto, article);

                MessageBox.Show("Товар успешно удалён.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при удалении товара: " + ex.Message, "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static BitmapImage LoadPhoto(string? photoPath)
        {
            if (string.IsNullOrWhiteSpace(photoPath))
            {
                return LoadResourceImage("picture.png");
            }

            string? diskPath = ResolveDiskPath(photoPath);
            if (!string.IsNullOrEmpty(diskPath) && File.Exists(diskPath))
            {
                try
                {
                    return LoadFromFileBytes(diskPath);
                }
                catch
                {
                    return LoadResourceImage("picture.png");
                }
            }

            return LoadResourceImage(Path.GetFileName(photoPath));
        }

        public static BitmapImage LoadFromFileBytes(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            BitmapImage image = new BitmapImage();

            using (MemoryStream ms = new MemoryStream(bytes))
            {
                image.BeginInit();
                image.StreamSource = ms;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
            }

            image.Freeze();
            return image;
        }

        public static string SaveResizedProductPhoto(string sourceFilePath, string article)
        {
            string imagesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
            Directory.CreateDirectory(imagesDirectory);

            BitmapImage sourceBitmap = LoadFromFileBytes(sourceFilePath);
            double scaleX = 300.0 / sourceBitmap.PixelWidth;
            double scaleY = 200.0 / sourceBitmap.PixelHeight;
            double scale = Math.Min(1.0, Math.Min(scaleX, scaleY));

            BitmapSource finalBitmap = sourceBitmap;
            if (scale < 1.0)
            {
                finalBitmap = new TransformedBitmap(sourceBitmap, new ScaleTransform(scale, scale));
            }

            string extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            string targetExtension = extension == ".png" ? ".png" : ".jpg";
            string safeArticle = string.Concat(article.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'));

            if (string.IsNullOrWhiteSpace(safeArticle))
            {
                safeArticle = "item";
            }

            string fileName = "product_" + safeArticle + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + targetExtension;
            string destinationPath = Path.Combine(imagesDirectory, fileName);

            BitmapEncoder encoder = targetExtension == ".png" ? new PngBitmapEncoder() : new JpegBitmapEncoder { QualityLevel = 90 };
            encoder.Frames.Add(BitmapFrame.Create(finalBitmap));

            using (FileStream fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(fs);
            }

            return "Images/" + fileName;
        }

        public static void DeleteProductPhotoIfUnused(ObuvDbContext db, string? oldPhotoPath, string currentArticle)
        {
            if (string.IsNullOrWhiteSpace(oldPhotoPath))
            {
                return;
            }

            string fileName = Path.GetFileName(oldPhotoPath);
            if (ProtectedImages.Any(p => string.Equals(p, fileName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            if (db.Products.Any(p => p.Article != currentArticle && p.Photo == oldPhotoPath))
            {
                return;
            }

            string? diskPath = ResolveDiskPath(oldPhotoPath);
            if (!string.IsNullOrEmpty(diskPath) && File.Exists(diskPath))
            {
                try
                {
                    File.Delete(diskPath);
                }
                catch
                {
                }
            }
        }

        private static string? ResolveDiskPath(string photoPath)
        {
            if (Path.IsPathRooted(photoPath))
            {
                return photoPath;
            }

            string directPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, photoPath);
            if (File.Exists(directPath))
            {
                return directPath;
            }

            string imagesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
            string inImagesPath = Path.Combine(imagesDirectory, Path.GetFileName(photoPath));
            if (File.Exists(inImagesPath))
            {
                return inImagesPath;
            }

            return directPath;
        }

        private static BitmapImage LoadResourceImage(string resourceFileName)
        {
            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri("pack://application:,,,/Images/" + resourceFileName, UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                BitmapImage fallback = new BitmapImage();
                fallback.BeginInit();
                fallback.UriSource = new Uri("pack://application:,,,/Images/picture.png", UriKind.Absolute);
                fallback.CacheOption = BitmapCacheOption.OnLoad;
                fallback.EndInit();
                fallback.Freeze();
                return fallback;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
