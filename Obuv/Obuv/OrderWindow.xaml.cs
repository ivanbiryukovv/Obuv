using Microsoft.EntityFrameworkCore;
using Obuv.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Obuv
{
    public partial class OrderWindow : Window
    {
        private readonly int? _orderId;
        private readonly Action? _onSaved;
        List<OrderItemRow> _orderItems = new List<OrderItemRow>();

        private bool IsEditMode => _orderId.HasValue;

        public OrderWindow(int orderId) : this(orderId, null)
        {
        }

        public OrderWindow(Action? onSaved)
        {
            _onSaved = onSaved;
            InitializeComponent();
            InitializeForm();
        }

        public OrderWindow(int orderId, Action? onSaved)
        {
            _orderId = orderId;
            _onSaved = onSaved;
            InitializeComponent();
            InitializeForm();
        }

        private void InitializeForm()
        {
            LoadDictionaries();

            if (IsEditMode)
            {
                Title = "ОБУВЬ - Редактирование заказа";
                HeaderTitleBlock.Text = "Редактирование заказа";
                OrderIdPanel.Visibility = Visibility.Visible;
                DeleteButton.Visibility = Visibility.Visible;
                LoadOrder();
            }
            else
            {
                Title = "ОБУВЬ - Добавление заказа";
                HeaderTitleBlock.Text = "Добавление заказа";
                OrderIdPanel.Visibility = Visibility.Collapsed;
                DeleteButton.Visibility = Visibility.Collapsed;
                StatusBox.SelectedIndex = 0;
                OrderDatePicker.SelectedDate = DateTime.Today;
                DeliveryDatePicker.SelectedDate = DateTime.Today.AddDays(7);
                LoadSuggestedCode();
            }
        }

        private void LoadDictionaries()
        {
            try
            {
                ObuvDbContext db = new ObuvDbContext();

                var users = db.Users.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).Select(u => new ComboItem { Id = u.UserId, Display = (u.LastName + " " + u.FirstName + " " + u.Surname).Trim() }).ToList();
                UserBox.ItemsSource = users;
                UserBox.DisplayMemberPath = "Display";
                UserBox.SelectedValuePath = "Id";

                var pickupPoints = db.PickupPoints.OrderBy(p => p.PickupPointId).Select(p => new ComboItem { Id = p.PickupPointId, Display = p.House.HasValue ? p.Index + ", г. " + p.City + ", ул. " + p.Street + ", д. " + p.House.Value : p.Index + ", г. " + p.City + ", ул. " + p.Street }).ToList();
                PickupPointBox.ItemsSource = pickupPoints;
                PickupPointBox.DisplayMemberPath = "Display";
                PickupPointBox.SelectedValuePath = "Id";

                var products = db.Products.OrderBy(p => p.ProductName).Select(p => new ProductComboItem { Article = p.Article, ProductName = p.ProductName, Display = p.Article + " — " + p.ProductName + " (" + p.Price.ToString("N2") + " руб.)" }).ToList();
                ProductSelectBox.ItemsSource = products;
                ProductSelectBox.DisplayMemberPath = "Display";
                if (products.Count > 0)
                {
                    ProductSelectBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки справочников заказа: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSuggestedCode()
        {
            try
            {
                ObuvDbContext db = new ObuvDbContext();
                int nextCode = db.Orders.Any() ? db.Orders.Max(o => o.Code) + 1 : 901;
                CodeBox.Text = nextCode.ToString();
            }
            catch
            {
                CodeBox.Text = "901";
            }
        }

        private void LoadOrder()
        {
            try
            {
                ObuvDbContext db = new ObuvDbContext();
                Order? order = db.Orders.Include(o => o.OrderProducts).ThenInclude(op => op.ArticleNavigation).FirstOrDefault(o => o.OrderId == _orderId!.Value);

                if (order == null)
                {
                    MessageBox.Show("Выбранный заказ не найден в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }

                OrderIdBox.Text = order.OrderId.ToString();
                CodeBox.Text = order.Code.ToString();
                UserBox.SelectedValue = order.UserId;
                PickupPointBox.SelectedValue = order.PickupPointId;
                OrderDatePicker.SelectedDate = order.OrderDate.ToDateTime(TimeOnly.MinValue);
                DeliveryDatePicker.SelectedDate = order.DeliveryDate.ToDateTime(TimeOnly.MinValue);

                string cleanStatus = order.Status.Trim();
                foreach (ComboBoxItem item in StatusBox.Items)
                {
                    if (string.Equals(item.Content?.ToString(), cleanStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        StatusBox.SelectedItem = item;
                        break;
                    }
                }

                _orderItems.Clear();
                foreach (var op in order.OrderProducts)
                {
                    _orderItems.Add(new OrderItemRow { Article = op.Article, ProductName = op.ArticleNavigation?.ProductName ?? op.Article, Count = op.Count });
                }

                RefreshOrderItemsList();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных заказа: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshOrderItemsList()
        {
            OrderItemsList.ItemsSource = null;
            OrderItemsList.ItemsSource = _orderItems;
        }

        private void AddItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProductSelectBox.SelectedItem is not ProductComboItem selectedProduct)
            {
                MessageBox.Show("Выберите товар для добавления в заказ.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(ItemCountBox.Text.Trim(), out int count) || count <= 0)
            {
                MessageBox.Show("Количество товара в заказе должно быть целым положительным числом.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                ItemCountBox.Focus();
                return;
            }

            OrderItemRow? existing = _orderItems.FirstOrDefault(i => i.Article == selectedProduct.Article);
            if (existing != null)
            {
                existing.Count += count;
            }
            else
            {
                _orderItems.Add(new OrderItemRow { Article = selectedProduct.Article, ProductName = selectedProduct.ProductName, Count = count });
            }

            RefreshOrderItemsList();
        }

        private void RemoveItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is OrderItemRow row)
            {
                _orderItems.Remove(row);
                RefreshOrderItemsList();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (StatusBox.SelectedItem is not ComboBoxItem statusItem)
            {
                MessageBox.Show("Выберите статус заказа.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusBox.Focus();
                return;
            }

            if (UserBox.SelectedValue is not int userId)
            {
                MessageBox.Show("Выберите клиента для заказа.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                UserBox.Focus();
                return;
            }

            if (PickupPointBox.SelectedValue is not int pickupPointId)
            {
                MessageBox.Show("Выберите пункт выдачи заказа.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                PickupPointBox.Focus();
                return;
            }

            if (!OrderDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Укажите дату заказа.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                OrderDatePicker.Focus();
                return;
            }

            if (!DeliveryDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Укажите дату доставки.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                DeliveryDatePicker.Focus();
                return;
            }

            DateOnly orderDate = DateOnly.FromDateTime(OrderDatePicker.SelectedDate.Value);
            DateOnly deliveryDate = DateOnly.FromDateTime(DeliveryDatePicker.SelectedDate.Value);

            if (deliveryDate < orderDate)
            {
                MessageBox.Show("Дата доставки не может быть раньше даты заказа.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                DeliveryDatePicker.Focus();
                return;
            }

            if (!int.TryParse(CodeBox.Text.Trim(), out int code) || code <= 0)
            {
                MessageBox.Show("Код для получения должен быть целым положительным числом.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                CodeBox.Focus();
                return;
            }

            if (_orderItems.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы один товар в состав заказа.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                ObuvDbContext db = new ObuvDbContext();
                Order order;
                if (IsEditMode)
                {
                    Order? existing = db.Orders.Include(o => o.OrderProducts).FirstOrDefault(o => o.OrderId == _orderId!.Value);
                    if (existing == null)
                    {
                        MessageBox.Show("Редактируемый заказ не найден в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    order = existing;
                    db.OrderProducts.RemoveRange(order.OrderProducts);
                }
                else
                {
                    order = new Order();
                    db.Orders.Add(order);
                }

                order.Status = statusItem.Content?.ToString() ?? "Новый";
                order.UserId = userId;
                order.PickupPointId = pickupPointId;
                order.OrderDate = orderDate;
                order.DeliveryDate = deliveryDate;
                order.Code = code;

                foreach (var item in _orderItems)
                {
                    order.OrderProducts.Add(new OrderProduct { Article = item.Article, Count = item.Count });
                }

                db.SaveChanges();

                _onSaved?.Invoke();
                MessageBox.Show(IsEditMode ? "Изменения заказа успешно сохранены." : "Новый заказ успешно добавлен.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения заказа: " + ex.Message, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsEditMode)
            {
                return;
            }

            if (TryDeleteOrder(_orderId!.Value))
            {
                _onSaved?.Invoke();
                this.Close();
            }
        }

        public static bool TryDeleteOrder(int orderId)
        {
            try
            {
                ObuvDbContext db = new ObuvDbContext();
                Order? order = db.Orders.Include(o => o.OrderProducts).FirstOrDefault(o => o.OrderId == orderId);

                if (order == null)
                {
                    MessageBox.Show("Заказ не найден в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                MessageBoxResult confirm = MessageBox.Show("Вы действительно хотите безвозвратно удалить заказ № " + orderId + "?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                {
                    return false;
                }

                db.OrderProducts.RemoveRange(order.OrderProducts);
                db.Orders.Remove(order);
                db.SaveChanges();

                MessageBox.Show("Заказ успешно удалён.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось удалить заказ: " + ex.Message, "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private class ComboItem
        {
            public int Id { get; set; }
            public string Display { get; set; }
        }

        private class ProductComboItem
        {
            public string Article { get; set; }
            public string ProductName { get; set; }
            public string Display { get; set; }
        }

        public class OrderItemRow
        {
            public string Article { get; set; }
            public string ProductName { get; set; }
            public int Count { get; set; }
            public string DisplayText => Article + " — " + ProductName + " (" + Count + " шт.)";
        }
    }
}
