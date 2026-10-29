using System;
using System.IO;
using System.Threading.Tasks;
using PlotWeave.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Chapters
{
    [RequireComponent(typeof(PlotPlayerHost))]
    public sealed class PlotChapterDemo : MonoBehaviour, IPlotChapterNavigation
    {
        public PlotPlaybackAsset content;
        public int entryNode = 2001;
        public bool persistProgress = true;
        public string saveFile = "plotweave-chapter-demo.json";
        public PlotChapterSession Session { get; private set; }
        public PlotLocalChapterBackend Backend { get; private set; }
        public int ReturnCount { get; private set; }
        public bool LastNewlyCompleted { get; private set; }
        private PlotChapterHostPlayer _player; private GameObject _menu; private TMP_Text _status; private Button _retry; private bool _destroyed;
        private void Start()
        {
            var host = GetComponent<PlotPlayerHost>(); host.playOnStart = false;
            if (content == null) throw new InvalidOperationException("章节示例缺少剧情内容");
            if (Path.GetFileName(saveFile) != saveFile || string.IsNullOrEmpty(saveFile)) throw new ArgumentException("示例存档只接受文件名");
            Backend = new PlotLocalChapterBackend(request => new PlotChapterContent(content.data, entryNode), persistProgress ? Path.Combine(Application.persistentDataPath, saveFile) : null);
            _player = new PlotChapterHostPlayer(host); Session = new PlotChapterSession(Backend, _player, this); Session.StateChanged += state => { if (_status != null) _status.text = "章节状态：" + state; }; BuildMenu();
        }
        private void BuildMenu()
        {
            var root = PlotPlayerVisual.Rect("ChapterMenu", transform); _menu = root.gameObject; var canvas = _menu.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 900; var scaler = _menu.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); _menu.AddComponent<GraphicRaycaster>();
            var panel = PlotPlayerVisual.Panel("MenuPanel", root, new Color(.045f, .075f, .13f)); PlotPlayerVisual.Fill(panel);
            Label("Title", panel, "PlotWeave · 章节宿主示例", 38, 280, 80); Label("Description", panel, "主线 / 日常 → 加载 → 剧情与分支 → 结算确认 → 返回\n本地示例保存进度，正式游戏替换后端与导航接口。", 24, 150, 100);
            var status = Label("Status", panel, "选择章节开始", 23, -80, 80); _status = status; Button("MainChapter", panel, "进入主线", -280, () => Run(PlotChapterKind.Main)); Button("DailyChapter", panel, "进入日常", 0, () => Run(PlotChapterKind.Daily)); _retry = Button("RetryReport", panel, "重试结算", 280, Retry); _retry.gameObject.SetActive(false); PlotPlayerVisual.EnsureInput();
        }
        private static TMP_Text Label(string name, Transform parent, string text, int size, float y, float height)
        { var rect = PlotPlayerVisual.Rect(name, parent); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(1350, height); rect.anchoredPosition = new Vector2(0, y); var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = PlotTMPText.DefaultFont; label.fontSize = size; label.text = text; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; return label; }
        private static Button Button(string name, Transform parent, string text, float x, Action click)
        { var button = PlotPlayerVisual.Button(name, parent, text); var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(240, 65); rect.anchoredPosition = new Vector2(x, 15); button.onClick.AddListener(() => click()); return button; }
        private async void Run(PlotChapterKind kind) { try { await Session.StartAsync(new PlotChapterRequest(kind, "demo-01")); } catch (Exception error) { if (!_destroyed) ShowFailure(error.Message, false); } }
        private async void Retry() { try { await Session.RetryReportAsync(); } catch (Exception error) { if (!_destroyed) ShowFailure(error.Message, true); } }
        public void Enter(PlotChapterRequest request) { if (_destroyed) return; _retry.gameObject.SetActive(false); _menu.SetActive(false); }
        public void Return(PlotChapterRequest request, PlotChapterExit reason, bool newlyCompleted)
        { if (_destroyed) return; ReturnCount++; LastNewlyCompleted = newlyCompleted; _menu.SetActive(true); _retry.gameObject.SetActive(false); _status.text = request.Kind + " / " + request.ChapterID + "：" + reason + (newlyCompleted ? " · 首次完成" : "") + "\n主线进度：" + Backend.IsCompleted(new PlotChapterRequest(PlotChapterKind.Main, "demo-01")) + "，日常进度：" + Backend.IsCompleted(new PlotChapterRequest(PlotChapterKind.Daily, "demo-01")); }
        public void ShowFailure(string message, bool canRetryReport) { if (_destroyed) return; _menu.SetActive(true); _status.text = message; _retry.gameObject.SetActive(canRetryReport); }
        private void OnDestroy() { _destroyed = true; Session?.Dispose(); _player?.Dispose(); }
    }
}
