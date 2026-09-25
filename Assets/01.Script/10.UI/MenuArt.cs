using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메뉴 공용 아트(Assets/Resources/MenuCommonArt) 를 불러오고 버튼에 입히는 도우미.
///   ButtonNormal / ButtonSelected : 네이비 바탕 + 금색 테두리 버튼 배경(9-slice).
///   Wood / Iron / Gold            : 투명 배경 자원 아이콘.
///
/// 9-slice: 스프라이트 border 는 임포터에서 픽셀 단위로 잡아 두고(2048x683 원본 기준),
/// 화면에서는 Image.pixelsPerUnitMultiplier 로 나눠 그린다. 이 값을 키울수록 테두리가 얇게 그려진다.
/// 이렇게 해야 버튼을 길게 늘여도 모서리 장식이 늘어나지 않는다.
/// </summary>
public static class MenuArt
{
    public const string Folder = "MenuCommonArt/";

    /// <summary>자원 순서: 0 목재, 1 철, 2 금화. 두 메뉴가 같은 순서를 쓴다.</summary>
    public static readonly string[] ResourceNames = { "목재", "철", "금화" };
    static readonly string[] ResourceFiles = { "Wood", "Iron", "Gold" };

    /// <summary>테두리를 그릴 때 나눌 값(클수록 테두리가 얇다). 원본이 2048px 라 크게 잡는다.</summary>
    public const float NormalBorderScale = 4f;
    public const float SelectedBorderScale = 6f;

    static Sprite _buttonNormal, _buttonSelected;
    static readonly Sprite[] _resourceIcons = new Sprite[3];

    public static Sprite ButtonNormal
    {
        get { if (_buttonNormal == null) _buttonNormal = Resources.Load<Sprite>(Folder + "ButtonNormal"); return _buttonNormal; }
    }

    public static Sprite ButtonSelected
    {
        get { if (_buttonSelected == null) _buttonSelected = Resources.Load<Sprite>(Folder + "ButtonSelected"); return _buttonSelected; }
    }

    public static Sprite ResourceIcon(int index)
    {
        if (index < 0 || index >= _resourceIcons.Length) return null;
        if (_resourceIcons[index] == null) _resourceIcons[index] = Resources.Load<Sprite>(Folder + ResourceFiles[index]);
        return _resourceIcons[index];
    }

    /// <summary>버튼 배경을 공용 아트로 바꾸고, 호버·눌림을 절제된 금색 계열로 맞춘다.</summary>
    public static void SkinButton(Button button, bool selected)
    {
        if (button == null) return;
        var image = button.targetGraphic as Image;
        if (image == null) image = button.GetComponent<Image>();
        if (image == null) return;

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.80f, 1f);   // 옅은 금빛
        colors.pressedColor = new Color(0.84f, 0.76f, 0.58f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.targetGraphic = image;

        SetSelected(image, selected);
    }

    /// <summary>선택 상태에 따라 버튼 배경 스프라이트를 바꾼다. 아트가 없으면 아무것도 하지 않는다.</summary>
    public static void SetSelected(Image image, bool selected)
    {
        if (image == null) return;
        Sprite sprite = selected ? ButtonSelected : ButtonNormal;
        if (sprite == null) return;
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Sliced;
        image.fillCenter = true;
        image.pixelsPerUnitMultiplier = selected ? SelectedBorderScale : NormalBorderScale;
    }

    /// <summary>버튼 라벨 색(선택 시 금색).</summary>
    public static Color LabelColor(bool selected)
    {
        return selected ? new Color(0.98f, 0.86f, 0.56f) : new Color(0.90f, 0.89f, 0.84f);
    }
}
