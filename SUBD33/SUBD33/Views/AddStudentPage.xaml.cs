using SUBD33.Data;
using SUBD33.Helpers;

namespace SUBD33.Views;

public partial class AddStudentPage : ContentPage
{
    private string _selectedPhotoPath = string.Empty;

    public AddStudentPage()
    {
        InitializeComponent();
    }

    private async void OnPickPhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(PickOptions.Images);

            if (result != null)
            {
                string targetDir = Path.Combine(FileSystem.AppDataDirectory, "Photos");
                Directory.CreateDirectory(targetDir);

                string destPath = Path.Combine(targetDir, $"{Guid.NewGuid()}_{result.FileName}");
                using (var sourceStream = await result.OpenReadAsync())
                using (var destStream = File.Create(destPath))
                {
                    await sourceStream.CopyToAsync(destStream);
                }

                _selectedPhotoPath = destPath;
                LblPhotoPath.Text = result.FileName;
                LblPhotoPath.TextColor = Color.FromArgb("#38BDF8");

                ImgPreview.Source = ImageSource.FromFile(_selectedPhotoPath);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Информация", $"Не удалось открыть системный проводник: {ex.Message}. Вы можете выбрать одно из готовых фото.", "OK");
        }
    }

    private void OnSelectPresetPhoto(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string photoName)
        {
            _selectedPhotoPath = photoName;
            LblPhotoPath.Text = photoName;
            LblPhotoPath.TextColor = Color.FromArgb("#38BDF8");
            ImgPreview.Source = ImageHelper.GetImageSource(_selectedPhotoPath);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        string fullName = TxtFullName.Text?.Trim() ?? string.Empty;
        string group = TxtGroup.Text?.Trim() ?? string.Empty;
        string courseText = TxtCourse.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(fullName))
        {
            await DisplayAlertAsync("Внимание", "Введите ФИО студента.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(group))
        {
            await DisplayAlertAsync("Внимание", "Введите группу студента.", "OK");
            return;
        }

        if (!int.TryParse(courseText, out int course) || course < 1 || course > 6)
        {
            await DisplayAlertAsync("Внимание", "Курс должен быть числом от 1 до 6.", "OK");
            return;
        }

        string photoToSave = string.IsNullOrWhiteSpace(_selectedPhotoPath) ? "avatar_default.png" : _selectedPhotoPath;

        try
        {
            bool success = DatabaseHelper.Instance.AddStudent(fullName, group, course, photoToSave);
            if (success)
            {
                await DisplayAlertAsync("Успех", $"Студент {fullName} успешно добавлен в базу данных!", "OK");
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlertAsync("Ошибка", "Не удалось сохранить студента в базу данных.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ошибка БД", $"Ошибка сохранения: {ex.Message}", "OK");
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
