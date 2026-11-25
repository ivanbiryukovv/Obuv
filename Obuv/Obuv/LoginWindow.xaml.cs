using Microsoft.EntityFrameworkCore;
using Obuv.Models;
using System;
using System.Linq;
using System.Windows;

namespace Obuv
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LoginBox.Text) && string.IsNullOrWhiteSpace(PassBox.Password))
            {
                MessageBox.Show("Пожалуйста, введите логин и пароль для входа в систему", "Вход не выполнен", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(LoginBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите логин для входа в систему", "Вход не выполнен", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(PassBox.Password))
            {
                MessageBox.Show("Пожалуйста, введите пароль для входа в систему", "Вход не выполнен", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                ObuvDbContext db = new ObuvDbContext();
                var user = db.Users.Include(u => u.Role).FirstOrDefault(u => u.Login == LoginBox.Text.Trim() && u.Password == PassBox.Password.Trim());

                if (user == null)
                {
                    MessageBox.Show("Неверно введен логин или пароль. Проверьте данные и повторите попытку.", "Вход не выполнен", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MainWindow mw = new MainWindow(user);
                mw.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось выполнить подключение к базе данных: " + ex.Message, "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MainWindow mw = new MainWindow(null);
            mw.Show();
            this.Close();
        }
    }
}
