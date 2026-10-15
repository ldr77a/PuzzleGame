using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// Hidden playtest tools, opened by long-pressing the stage title: hand the device to the next tester
    /// (clears progress after a second press) or read the per-tester summary of the playtest log.
    /// </summary>
    sealed class PlaytestPanel
    {
        readonly GameObject root;
        readonly Text body;
        readonly Text nextLabel;
        readonly Action nextTester;
        readonly string startsFrom;
        bool confirming;

        public bool IsVisible => root.activeSelf;

        /// <param name="startsFrom">
        /// Where play resumes after "다음 테스터", completing "…지워지고 {startsFrom} 시작해요", e.g. "1번 스테이지부터"
        /// or "정원에서".
        /// </param>
        public PlaytestPanel(RectTransform parent, Action nextTester, Func<string> summary, string startsFrom)
        {
            this.nextTester = nextTester;
            this.startsFrom = startsFrom;

            RectTransform layer = Widgets.Layer(parent, "Playtest Panel");
            root = layer.gameObject;
            Widgets.Dim(layer, "Dim"); // swallows taps meant for the board underneath, out to the screen edges

            Image card = Ui.NewImage("Card", layer, SpriteFactory.RoundedRect, Theme.Panel);
            Ui.Box(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 1200));

            // Jua has a single weight, so no FontStyle.Bold here (it only fakes bold).
            Text title = Ui.NewLabel("Title", card.transform, "플레이테스트", 64, Theme.Ink, TextAnchor.MiddleCenter);
            Ui.TopStrip(title.rectTransform, 50f, 100f, 40f);

            body = Ui.NewLabel("Body", card.transform, "", 36, Theme.Muted, TextAnchor.UpperLeft);
            body.verticalOverflow = VerticalWrapMode.Truncate;
            body.resizeTextForBestFit = true; // long summaries shrink instead of spilling out of the card
            body.resizeTextMinSize = 20;
            body.resizeTextMaxSize = 36;
            Ui.Stretch(body.rectTransform, 60f, 360f, 60f, 190f);

            Button nextButton = Ui.NewButton("Next Tester", card.transform, "", 44, Theme.Accent, Color.white, OnNextTester, out nextLabel);
            nextLabel.fontStyle = FontStyle.Normal;
            Ui.Box(nextButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(800, 120));
            Button summaryButton = Ui.NewButton("Summary", card.transform, "기록 보기", 44, Theme.Background, Theme.Ink,
                () => body.text = summary(), out Text summaryLabel);
            summaryLabel.fontStyle = FontStyle.Normal;
            Ui.Box(summaryButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(60, 50), new Vector2(390, 120));
            Button closeButton = Ui.NewButton("Close", card.transform, "닫기", 44, Theme.Background, Theme.Ink, Hide, out Text closeLabel);
            closeLabel.fontStyle = FontStyle.Normal;
            Ui.Box(closeButton.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(-60, 50), new Vector2(390, 120));

            root.SetActive(false);
        }

        public void Show(int tester)
        {
            confirming = false;
            nextLabel.text = "다음 테스터";
            body.text = $"지금 테스터 {tester}번\n\n" +
                        $"다음 사람에게 넘길 때 \"다음 테스터\"를 누르세요. 진행, 별, 힌트가 처음 상태로 돌아가고 {startsFrom} 시작해요. " +
                        "기록은 지워지지 않아요.\n\n마지막 사람이 끝났을 때도 한 번 눌러야 그 사람의 마지막 판까지 기록돼요.";
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide() => root.SetActive(false);

        void OnNextTester()
        {
            if (!confirming)
            {
                confirming = true;
                nextLabel.text = "한 번 더 누르면 초기화";
                body.text = $"진행, 별, 힌트를 모두 처음 상태로 돌리고 {startsFrom} 다시 시작할까요?\n\n취소하려면 \"닫기\"를 누르세요.";
                return;
            }
            Hide();
            nextTester();
        }
    }
}
