using Microsoft.EntityFrameworkCore;
using Obuv.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Obuv
{
    public partial class OrdersWindow : Window
    {
        private readonly User _currentUser;
        OrderWindow? _openOrderWindow;

        private bool IsAdmin => _currentUser.RoleId == 1 || string.Equals(_currentUser.Role?.Role1, "Администратор", StringComparison.OrdinalIgnoreCase);

        public OrdersWindow(User currentUser)
        {
            _currentUser = currentUser;
            InitializeComponent();

            UserNameBlock.Text = _currentUser.LastName + " " + _currentUser.FirstName + " " + _currentUser.Surname;
            AdminActionsPanel.Visibility = IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            LoadOrders();
        }

        public void LoadOrders()
        {
            List<OrderView> orderViews = new List<OrderView>();

            try
            {
                ObuvDbContext db = new ObuvDbContext();

                List<Order> orders = db.Orders.Include(o => o.User).Include(o => o.PickupPoint).Include(o => o.OrderProducts).OrderBy(o => o.OrderId).ToList();

                Visibility deleteVisibility = IsAdmin ? Visibility.Visible : Visibility.Collapsed;
                Cursor cardCursor = IsAdmin ? Cursors.Hand : Cursors.Arrow;

                foreach (var order in orders)
                {
                    string articlesText = order.OrderProducts.Count > 0 ? string.Join(", ", order.OrderProducts.Select(op => op.Article + " (" + op.Count + " шт.)")) : "Нет товаров";
                    string housePart = order.PickupPoint.House.HasValue ? ", д. " + order.PickupPoint.House.Value : string.Empty;
                    string pickupAddress = order.PickupPoint.Index + ", г. " + order.PickupPoint.City + ", ул. " + order.PickupPoint.Street + housePart;
                    string clientName = (order.User.LastName + " " + order.User.FirstName + " " + order.User.Surname).Trim();

                    orderViews.Add(new OrderView
                    {
                        OrderId = order.OrderId,
                        OrderTitle = "Заказ № " + order.OrderId,
                        ArticlesText = "Состав заказа: " + articlesText,
                        StatusText = "Статус заказа: " + order.Status.Trim(),
                        ClientNameText = "Клиент: " + clientName,
                        PickupPointText = "Адрес пункта выдачи: " + pickupAddress,
                        OrderDateText = "Дата заказа: " + order.OrderDate.ToString("dd.MM.yyyy"),
                        CodeText = "Код получения: " + order.Code,
                        DeliveryDateText = order.DeliveryDate.ToString("dd.MM.yyyy"),
                        DeleteButtonVisibility = deleteVisibility,
                        CardCursor = cardCursor
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки заказов: " + ex.Message, "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            OrdersList.ItemsSource = null;
            OrdersList.ItemsSource = orderViews;

            EmptyOrdersBlock.Visibility = orderViews.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenOrderEditor(int? orderId)
        {
            if (!IsAdmin)
            {
                MessageBox.Show("Добавление и редактирование заказов доступно только администратору.", "Доступ ограничен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_openOrderWindow != null)
            {
                _openOrderWindow.Activate();
                MessageBox.Show("Окно добавления или редактирования заказа уже открыто. Закройте текущее окно перед открытием нового.", "Окно уже открыто", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OrderWindow ow = orderId.HasValue ? new OrderWindow(orderId.Value, LoadOrders) : new OrderWindow(LoadOrders);
            ow.Owner = this;
            ow.Closed += (s, args) => { _openOrderWindow = null; };
            _openOrderWindow = ow;
            ow.Show();
        }

        private void OrdersList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source && FindParent<Button>(source) != null)
            {
                return;
            }

            if (sender is FrameworkElement fe && fe.DataContext is OrderView view)
            {
                OpenOrderEditor(view.OrderId);
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

        private void AddOrderButton_Click(object sender, RoutedEventArgs e)
        {
            OpenOrderEditor(null);
        }

        private void DeleteOrderButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (!IsAdmin)
            {
                MessageBox.Show("Удаление заказов доступно только администратору.", "Доступ ограничен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_openOrderWindow != null)
            {
                _openOrderWindow.Activate();
                MessageBox.Show("Сначала закройте открытое окно редактирования заказа.", "Окно уже открыто", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (sender is FrameworkElement fe && fe.DataContext is OrderView view)
            {
                if (OrderWindow.TryDeleteOrder(view.OrderId))
                {
                    LoadOrders();
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_openOrderWindow != null)
            {
                _openOrderWindow.Close();
            }

            this.Close();
        }
    }

    public class OrderView
    {
        public int OrderId { get; set; }
        public string OrderTitle { get; set; }
        public string ArticlesText { get; set; }
        public string StatusText { get; set; }
        public string ClientNameText { get; set; }
        public string PickupPointText { get; set; }
        public string OrderDateText { get; set; }
        public string CodeText { get; set; }
        public string DeliveryDateText { get; set; }
        public Visibility DeleteButtonVisibility { get; set; }
        public Cursor CardCursor { get; set; }
    }
}
