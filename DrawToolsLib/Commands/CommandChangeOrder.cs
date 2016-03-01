using System.Collections.Generic;
using System.Windows.Media;
using System.Linq;

namespace DrawToolsLib
{
    /// <summary>
    /// Changing objects order: move to front/back.
    /// Command keeps list of object IDs before and after operation.
    /// Using these lists, it is possible to undo/redo Move to ... operation.
    /// </summary>
    internal class CommandChangeOrder : CommandBase
    {
        private int[] _listBefore;
        private int[] _listAfter;

        // Create this command BEFORE operation.
        public CommandChangeOrder(DrawingCanvas drawingCanvas)
        {
            _listBefore = drawingCanvas.GraphicsList
                .OfType<GraphicsVisual>()
                .Select(g => g.ObjectId)
                .ToArray();
        }

        // Call this function AFTER operation.
        public void NewState(DrawingCanvas drawingCanvas)
        {
            _listAfter = drawingCanvas.GraphicsList
                .OfType<GraphicsVisual>()
                .Select(g => g.ObjectId)
                .ToArray();
        }

        public override void Undo(DrawingCanvas drawingCanvas)
        {
            SetCanvasOrder(drawingCanvas.GraphicsList, _listBefore);
        }

        public override void Redo(DrawingCanvas drawingCanvas)
        {
            SetCanvasOrder(drawingCanvas.GraphicsList, _listAfter);
        }

        private static void SetCanvasOrder(VisualCollection graphicsList, int[] indexList)
        {
            List<GraphicsVisual> tmpList = new List<GraphicsVisual>();

            // Read indexList, find every element in graphicsList by ID
            // and move it to tmpList.

            foreach (int id in indexList)
            {
                GraphicsVisual objectToMove = null;
                foreach (GraphicsVisual g in graphicsList)
                {
                    if (g.ObjectId == id)
                    {
                        objectToMove = g;
                        break;
                    }
                }
                if (objectToMove != null)
                {
                    tmpList.Add(objectToMove);
                    graphicsList.Remove(objectToMove);
                }
            }

            // Now tmpList contains objects in correct order.
            // Read tmpList and add all its elements back to graphicsList.
            foreach (GraphicsVisual g in tmpList)
            {
                graphicsList.Add(g);
            }
        }
    }
}