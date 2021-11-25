using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data;
using Furball.Engine.Engine.Graphics.Drawables;
using Furball.Engine.Engine.Helpers;

namespace pTyping.UiGenerator {
    public class UiContainer : CompositeDrawable {
        public Bindable<OriginType> ElementOriginType;

        private readonly ObservableCollection<UiElement> _elements = new();

        public ReadOnlyCollection<UiElement> Elements => new(this._elements);

        public int EasingTime = 100;

        /// <summary>
        ///     Creates a UiContainer
        /// </summary>
        /// <param name="originType">The origin type for the internal elements</param>
        public UiContainer(OriginType originType) {
            this.ElementOriginType = new(originType);

            this._elements.CollectionChanged += this.Recalculate;
            this.ElementOriginType.OnChange  += this.OnOriginTypeChange;
        }

        private void OnOriginTypeChange(object sender, OriginType e) {
            this.Recalculate(null, null);
        }

        private readonly Queue<UiElement> _queuedForDeletion = new();
        private void Recalculate(object sender, NotifyCollectionChangedEventArgs e) {
            float y = 0f;

            UiElement removedElement = e.Action == NotifyCollectionChangedAction.Remove ? e.OldItems?[0] as UiElement : null;

            if (removedElement != null)
                this._drawables.Remove(removedElement.Drawable);

            for (int i = 0; i < this._elements.Count; i++) {
                UiElement element = this._elements[i];

                if (element == null)
                    throw new NoNullAllowedException("UiElement cannot be null!");

                //TODO: support when multiple are added
                bool elementIsAdded = e.Action == NotifyCollectionChangedAction.Add && e.NewItems?[0] == element;

                //Update the origin type
                element.Drawable.OriginType = this.ElementOriginType;

                element.Drawable.MoveTo(new(0, y), elementIsAdded ? 0 : this.EasingTime);

                if (elementIsAdded)
                    element.Drawable.FadeInFromZero(this.EasingTime);

                //Update the global y
                y += element.Drawable.Size.Y + element.SpaceAfter;

                //Only add the new element
                if (elementIsAdded)
                    this._drawables.Add(element.Drawable);
            }
        }

        public void RegisterElement(UiElement element) {
            if (element.InUse || element.Drawable == null)
                throw new InvalidOperationException();

            element.InUse = true;

            this._elements.Add(element);
        }

        public void UnRegisterElement(UiElement element) {
            element.InUse = false;

            this._elements.Remove(element);
        }

        public override void Dispose(bool disposing) {
            this._elements.CollectionChanged -= this.Recalculate;
            this.ElementOriginType.OnChange  -= this.OnOriginTypeChange;

            base.Dispose(disposing);
        }
    }
}
