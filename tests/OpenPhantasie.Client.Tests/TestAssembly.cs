// s174: the Client's text and fonts are process-wide (UiText.Table, PixelFont's pack font, the screen layout); test classes
// that set them raced each other when run in parallel (layout tests failed now and then, more often in the public tree,
// which CI runs). One class at a time.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
