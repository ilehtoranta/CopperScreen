'use strict';

(() => {
  const links = [...document.querySelectorAll('[data-gallery] a[data-image]')];
  const viewer = document.querySelector('#image-viewer');
  if (!links.length || !viewer || typeof viewer.showModal !== 'function') return;
  const image = viewer.querySelector('img');
  const title = viewer.querySelector('#viewer-title');
  const caption = viewer.querySelector('#viewer-caption');
  const counter = viewer.querySelector('[data-counter]');
  const original = viewer.querySelector('[data-original]');
  const error = viewer.querySelector('[data-error]');
  let selected = 0;
  let opener = null;

  function display(index) {
    selected = (index + links.length) % links.length;
    const link = links[selected];
    title.textContent = link.dataset.title;
    caption.textContent = link.dataset.caption;
    image.alt = link.querySelector('img').alt;
    error.hidden = true;
    image.src = link.href;
    original.href = link.href;
    counter.textContent = `${selected + 1} / ${links.length}`;
  }
  function open(index, link) {
    opener = link;
    display(index);
    viewer.showModal();
    document.documentElement.classList.add('viewer-open');
  }
  links.forEach((link, index) => link.addEventListener('click', event => {
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    open(index, link);
  }));
  viewer.querySelector('[data-close]').addEventListener('click', () => viewer.close());
  viewer.querySelector('[data-prev]').addEventListener('click', () => display(selected - 1));
  viewer.querySelector('[data-next]').addEventListener('click', () => display(selected + 1));
  viewer.addEventListener('keydown', event => {
    if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
      event.preventDefault();
      display(selected + (event.key === 'ArrowLeft' ? -1 : 1));
    }
  });
  viewer.addEventListener('click', event => {
    if (event.target !== viewer) return;
    const bounds = viewer.getBoundingClientRect();
    if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) viewer.close();
  });
  viewer.addEventListener('close', () => {
    document.documentElement.classList.remove('viewer-open');
    opener?.focus({ preventScroll: true });
  });
  image.addEventListener('error', () => { error.hidden = false; });
  if (links.length < 2) {
    viewer.querySelector('[data-prev]').hidden = true;
    viewer.querySelector('[data-next]').hidden = true;
  }
  const initial = links.findIndex(link => `#${link.dataset.id}` === location.hash);
  if (initial >= 0) open(initial, links[initial]);
})();
