// What happens to an ItemSource after it has been used.
public enum UseMode
{
    // Nothing visible changes.
    None,

    // The whole object is destroyed.
    Destroy,

    // The assigned visual child is hidden and the rest stays, such as the fruit on a tree.
    HideVisual
}