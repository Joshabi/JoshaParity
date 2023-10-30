using System;
using System.Collections.Generic;
using System.Linq;

namespace JoshaParity
{

    /// <summary>
    /// Type of swing composition
    /// </summary>
    public enum SwingType { 
        Normal,
        Stack,
        Window,
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
            if (_notesBuffer.Count == 1) {
                if (_noMoreData) {
                    if (_notesBuffer[0] is BurstSlider)
                    {
                        BurstSlider slider = (BurstSlider)_notesBuffer[0];
                        _notesBuffer.Add(new Note { x = slider.tx, y = slider.ty, c = slider.c, d = 8, b = slider.tb });
                        return (SwingType.Chain, _notesBuffer);
                    }
                    else { return (SwingType.Normal, new() { _notesBuffer[0] }); }
                }
                return (SwingType.Undecided, new());
            }

            Note currentNote = _notesBuffer[_notesBuffer.Count-2];
            Note nextNote = _notesBuffer[_notesBuffer.Count-1];

            const float sliderPrecision = 59f; // In miliseconds
            float timeDiff = Math.Abs(currentNote.ms - nextNote.ms);
            if (timeDiff > sliderPrecision && currentNote is not BurstSlider)
            {
                if (IsStack()) {
                    // Attempt to sort snapped swing if not all dots
                    if (_notesBuffer.Count > 1 && _notesBuffer.All(x => x.b == _notesBuffer[0].b)) _notesBuffer = SwingUtils.SnappedSwingSort(_notesBuffer);
                    return (SwingType.Stack, new()); 
                }
                if (IsSlider()) { return (SwingType.Slider, new()); }
                if (IsDotSpam()) { return (SwingType.DotSpam, new()); }
            } else if (timeDiff > sliderPrecision && currentNote is BurstSlider)
            { 
                BurstSlider slider = (BurstSlider)currentNote;
                _notesBuffer.Add(new Note { x = slider.tx, y = slider.ty, c = slider.c, d = 8, b = slider.tb });
                return (SwingType.Chain, _notesBuffer);
            }
            else { return (SwingType.Undecided, _notesBuffer); }
            return (SwingType.Undecided, _notesBuffer);
        }

        private bool IsStack() { 
            // REQUIREMENTS:
            // - All same snap
            if (_notesBuffer.All(x => x.ms == _notesBuffer[0].ms)) { 
                // - If not all dots, and not all same angle then wonky stack
                if (!_notesBuffer.All(x => x.d == 8) && _notesBuffer.All(x => x.d != _notesBuffer[0].d)) {
                    return false;
                }
                return true; 
            }
            else { return false; }
        }
        private bool IsSlider() {
            float startingRotation = ParityUtils.ForehandDict(true)[_notesBuffer[0].d];
            foreach (Note note in _notesBuffer) {
                // If starting rotation to latest exceeds 45 we kill this swing
                // If from the last note the change is more than 45 we kill it
                if (Math.Abs(startingRotation - ParityUtils.ForehandDict(true)[note.d]) > 45) {
                    return false; } }
            return true;
        }
        private bool IsDotSpam() { return false; }
    }
}
