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
        public List<Note> _notesBuffer = new List<Note>();
        private bool _noMoreData = false;

        // Used for updating the buffer and clearing it
        public void ClearBuffer() { _notesBuffer.Clear(); }
        public void OpenBuffer() { _noMoreData = false; }
        public void EndBuffer() { _noMoreData = true; }

        public (SwingType type, List<Note> notes) UpdateBuffer(Note nextNote)
        {
            // If first note, add and return
            if (_notesBuffer.Count == 0) {
                _notesBuffer.Add(nextNote);
                if (_notesBuffer[0] is BurstSlider BSNote) {
                    _notesBuffer.Add(new Note { x = BSNote.tx, y = BSNote.ty, c = BSNote.c, d = 8, b = BSNote.tb });
                    return (SwingType.Chain, new(_notesBuffer));
                }
                return (SwingType.Undecided, new(_notesBuffer));
            }

            if (_noMoreData) { _notesBuffer.Add(nextNote); }

            Note currentNote = _notesBuffer[_notesBuffer.Count-1];
            const float sliderPrecision = 59f; // In miliseconds
            float timeDiff = Math.Abs(currentNote.ms - nextNote.ms);
            if (timeDiff <= sliderPrecision && currentNote is not BurstSlider)
            {
                if (nextNote.d == 8 || currentNote.d == 8 ||
                    currentNote.d == nextNote.d || Math.Abs(ParityUtils.ForehandDict(true)[currentNote.d] - ParityUtils.ForehandDict(true)[nextNote.d]) <= 45 ||
                     Math.Abs(ParityUtils.BackhandDict(true)[currentNote.d] - ParityUtils.BackhandDict(true)[nextNote.d]) <= 45)
                { _notesBuffer.Add(nextNote); return (SwingType.Undecided, new(_notesBuffer)); }
            }

            List<Note> constructedSwing = new List<Note>(_notesBuffer);
            ClearBuffer();
            _notesBuffer.Add(nextNote);
            return (SwingType.Normal, new(constructedSwing));
        }

        private bool IsStack() { 
            // REQUIREMENTS:
            // - All same snap
            if (_notesBuffer.All(x => x.ms == _notesBuffer[0].ms)) { 
                // - If not all dots, and not all same angle then wonky stack
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
