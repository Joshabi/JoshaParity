﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

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
        public List<Note> _constructedSwing = new List<Note>();
        private bool _noMoreData = false;

        // Used for updating the buffer and clearing it
        public void ClearBuffer() { _notesBuffer.Clear(); }
        public void OpenBuffer() { _noMoreData = false; }
        public void EndBuffer() { _noMoreData = true; }

        /// <summary>
        /// Updates the current notes buffer and attempts to compose a swing from it
        /// </summary>
        /// <param name="nextNote">Note to add to the buffer</param>
        /// <returns></returns>
        public (SwingType type, List<Note> notes) UpdateBuffer(Note nextNote)
        {
            if (!_noMoreData)
            {
                // If first note, add and return
                if (_notesBuffer.Count == 0)
                {
                    _notesBuffer.Add(nextNote);
                    if (_notesBuffer[0] is BurstSlider BSNote)
                    {
                        _notesBuffer.Add(new Note { x = BSNote.tx, y = BSNote.ty, c = BSNote.c, d = 8, b = BSNote.tb });
                        return (SwingType.Chain, new(_notesBuffer));
                    }
                    return (SwingType.Undecided, new(_notesBuffer));
                }

                // Get current note check if slider precision applies
                Note currentNote = _notesBuffer[_notesBuffer.Count - 1];
                const float sliderPrecision = 59f; // In miliseconds
                float timeDiff = Math.Abs(currentNote.ms - nextNote.ms);
                if (timeDiff <= sliderPrecision && currentNote is not BurstSlider)
                {
                    if (nextNote.d == 8 || currentNote.d == 8 ||
                        currentNote.d == nextNote.d || Math.Abs(ParityUtils.ForehandDict(true)[currentNote.d] - ParityUtils.ForehandDict(true)[nextNote.d]) <= 45 ||
                         Math.Abs(ParityUtils.BackhandDict(true)[currentNote.d] - ParityUtils.BackhandDict(true)[nextNote.d]) <= 45)
                    { _notesBuffer.Add(nextNote); return (SwingType.Undecided, new(_notesBuffer)); }
                }

                _constructedSwing = new List<Note>(_notesBuffer);
                ClearBuffer();
                _notesBuffer.Add(nextNote);
            } else
            {
                _notesBuffer.Add(nextNote);
                _constructedSwing = new List<Note>(_notesBuffer);
            }

            // Stack classification
            SwingType returnType = SwingType.Normal;
            if (_constructedSwing.All(x => Math.Abs(_constructedSwing[0].b) - x.b < 0.01f) && _constructedSwing.Count > 1)
            {
                if (IsStack()) { returnType = SwingType.Stack; }
                if (IsWindow()) { returnType = SwingType.Window; }
            }
            else { if (_constructedSwing.Count > 1) { returnType = SwingType.Slider; } }
            return (returnType, new(_constructedSwing));
        }

        /// <summary>
        /// Attempts to classify notes as a stack
        /// </summary>
        /// <returns></returns>
        private bool IsStack() {
            Note lastNote = _constructedSwing[0];
            for (int i = 1; i < _constructedSwing.Count; i++) {
                // If distance between notes in sequence is > 1.414
                Note nextNote = _constructedSwing[i];
                if (Math.Abs(nextNote.x - lastNote.x) > 1 || Math.Abs(nextNote.y - lastNote.y) > 1) { return false; }
            }
            return true;
        }

        /// <summary>
        /// Attempts to classify notes as a Window
        /// </summary>
        /// <returns></returns>
        private bool IsWindow() {
            Note lastNote = _constructedSwing[0];
            for (int i = 1; i < _constructedSwing.Count; i++)
            {
                // If distance between notes in sequence is > 1.414
                Note nextNote = _constructedSwing[i];
                if (Math.Abs(nextNote.x - lastNote.x) > 1 || Math.Abs(nextNote.y - lastNote.y) > 1) { return true; }
            }
            return false;
        }

        /// <summary>
        /// Attempts to classify notes as Dot Spam [Not implemented]
        /// </summary>
        /// <returns></returns>
        private bool IsDotSpam() {
            return false;
        }
    }
}
