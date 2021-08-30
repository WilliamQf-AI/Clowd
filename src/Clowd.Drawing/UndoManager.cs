﻿using System;
using System.Collections.Generic;

namespace Clowd.Drawing
{
    internal class UndoManager
    {
        public bool CanUndo => _position >= 0 && _position <= _historyList.Count - 1;
        public bool CanRedo => _position < _historyList.Count - 1;
        public event EventHandler StateChanged;

        private readonly DrawingCanvas _drawingCanvas;
        private List<byte[]> _historyList;
        private int _position;

        public UndoManager(DrawingCanvas drawingCanvas)
        {
            this._drawingCanvas = drawingCanvas;
            ClearHistory();
        }

        public void ClearHistory()
        {
            _historyList = new List<byte[]>();
            _position = -1;
            RaiseStateChangedEvent();
        }

        static bool ByteArrayCompare(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
        {
            // fastest way to compare two arrays without a p/invoke to memcmp
            return a1.SequenceEqual(a2);
        }

        public void SetFirstStep()
        {
            ClearHistory();
            AddCommandStep();
        }

        public void AddCommandStep()
        {
            var state = _drawingCanvas.GraphicsList.SerializeObjects(false);

            // skip duplicates
            if (_position >= 0 && ByteArrayCompare(_historyList[_position], state))
                return;

            this.TrimHistoryList();
            _historyList.Add(state);
            _position++;
            RaiseStateChangedEvent();
        }

        public void Undo()
        {
            if (!CanUndo)
                return;

            var nextState = _historyList[_position--];
            var nextGraphics = new GraphicCollection(_drawingCanvas, _drawingCanvas.CanvasUiElementScale);
            nextGraphics.DeserializeObjectsInto(nextState);

            // replace the current graphic set with the one from history
            var old = _drawingCanvas.GraphicsList;
            _drawingCanvas.GraphicsList = nextGraphics;
            _drawingCanvas.InvalidateVisual();
            old.Clear();

            RaiseStateChangedEvent();
        }

        public void Redo()
        {
            if (!CanRedo)
                return;

            int itemToRedo = _position + 1;

            var nextState = _historyList[itemToRedo];
            var nextGraphics = new GraphicCollection(_drawingCanvas, _drawingCanvas.CanvasUiElementScale);
            nextGraphics.DeserializeObjectsInto(nextState);

            // replace the current graphic set with the one from history
            var old = _drawingCanvas.GraphicsList;
            _drawingCanvas.GraphicsList = nextGraphics;
            _drawingCanvas.InvalidateVisual();
            old.Clear();

            _position++;
            RaiseStateChangedEvent();
        }

        private void TrimHistoryList()
        {
            // We can redo any undone command until we execute a new 
            // command. The new command takes us off in a new direction,
            // which means we can no longer redo previously undone actions. 
            // So, we purge all undone commands from the history list.*/

            if (_historyList.Count == 0)
                return;
            if (_position == _historyList.Count - 1)
                return;

            // Purge all items below the NextUndo pointer
            for (int i = _historyList.Count - 1; i > _position; i--)
                _historyList.RemoveAt(i);
        }

        private void RaiseStateChangedEvent()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
