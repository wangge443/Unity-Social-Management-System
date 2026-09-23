using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SocialSystem.Client.Posts
{
    // At a comment viewport boundary, keep the surrounding feed scrollable.
    public sealed class PostCommentsScrollRect : ScrollRect
    {
        public ScrollRect Outer { get; set; }
        public override void OnScroll(PointerEventData eventData)
        {
            var fits = content == null || viewport == null || content.rect.height <= viewport.rect.height;
            var outward = (eventData.scrollDelta.y > 0 && verticalNormalizedPosition >= 0.999f) ||
                (eventData.scrollDelta.y < 0 && verticalNormalizedPosition <= 0.001f);
            if (Outer != null && (fits || outward)) Outer.OnScroll(eventData);
            else base.OnScroll(eventData);
        }
    }
}
