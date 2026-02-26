using System.Collections.Generic;
using PhotoBooth.Core.Models;

namespace PhotoBooth.UI.Services;

/// <summary>
/// Manages the current session state across all views.
/// </summary>
public class SessionService
{
    public Session CurrentSession { get; private set; } = new();

    public void StartNewSession()
    {
        CurrentSession = new Session();
    }

    public void SetLayout(Layout layout)
    {
        CurrentSession.SelectedLayout = layout;
    }

    public void SetFrame(Frame frame)
    {
        CurrentSession.SelectedFrame = frame;
    }

    public void SetBackground(Background background)
    {
        CurrentSession.SelectedBackground = background;
    }

    public void AddCapturedPhoto(string path)
    {
        CurrentSession.CapturedPhotoPaths.Add(path);
    }

    public void SetSelectedPhotos(List<int> indices)
    {
        CurrentSession.SelectedPhotoIndices = indices;
    }

    public void AddSticker(StickerPlacement sticker)
    {
        CurrentSession.Stickers.Add(sticker);
    }

    public void SetPaymentComplete(bool isPaid)
    {
        CurrentSession.IsPaid = isPaid;
    }

    public void SetFinalImage(string path)
    {
        CurrentSession.FinalImagePath = path;
    }

    public void SetQRCode(string url)
    {
        CurrentSession.QRCodeUrl = url;
    }
}
