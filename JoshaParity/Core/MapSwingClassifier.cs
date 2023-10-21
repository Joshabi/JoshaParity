using System.Collections.Generic;

namespace JoshaParity
{

    /// <summary>
    /// Type of swing composition
    /// </summary>
    public enum SwingType { 
        Normal,
        Stack,
        Slider,
        Chain,
        DotSpam,
        Undecided
    }

    /// <summary>
    /// Given a buffer of notes, provides a classification for how it would be swung as
    /// </summary>
    public class MapSwingClassifier
    {
        // Notes currently being examined for classification
        // Current composition of swing in construction
        private List<Note> _notesBuffer = new List<Note>();
        public List<Note> constructedSwingNotes = new List<Note>();
        private bool _noMoreData = false;

        // Used for updating the buffer and clearing it
        public void UpdateBuffer(Note note) { _notesBuffer.Add(note); }
        public void UpdateBuffer(List<Note> newNotes) { _notesBuffer.AddRange(newNotes); }
        public void ClearBuffer() { _notesBuffer.Clear(); }
        public void OpenBuffer() { _noMoreData = false; }
        public void EndBuffer() { _noMoreData = true; }

        public (SwingType type, List<Note> notes) ClassifyBuffer()
        {
            // If no notes (shouldn't be possible), return Undecided
            // If only one note in buffer, and buffer is now closed, return normal;
            if (_notesBuffer.Count == 0) return (SwingType.Undecided, new());
            if (_notesBuffer.Count == 1 && _noMoreData) {
                if (_notesBuffer[0] is BurstSlider) { return (SwingType.Chain, new() { _notesBuffer[0] }); }
                else { return (SwingType.Normal, new() { _notesBuffer[0] }); }
            }


            // Need to add all classification code still
            return (SwingType.Undecided, _notesBuffer);
        }
    }
}
