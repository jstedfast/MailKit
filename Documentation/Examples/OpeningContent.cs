using (var stream = part.Content.Open ()) {
    // At this point, you can now read from the stream as if it were the original,
    // raw content. Assuming you have an image UI control that could load from a
    // stream, you could do something like this:
    imageControl.Load (stream);
}
