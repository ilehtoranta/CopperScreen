# Adaptive controls

CopperScreen defaults to a right-side control panel in windows at least 920
logical pixels wide. Session and disk controls are at the top; Fullscreen,
Sound, Inputs, Settings and More sit at the bottom. Narrower windows use compact
horizontal controls. The launcher uses the same layout.

The initial window is 1040 by 760 logical pixels, bounded by the monitor's work
area. The Amiga picture retains the presenter's existing pixel-aspect correction
and uniform scaling within the available space.

F11 shows or hides controls in both windowed and fullscreen modes. In windowed
mode, hiding controls frees their space for the display and preserves the window
size. The windowed visibility choice survives fullscreen transitions. Fullscreen
starts with controls hidden and displays them as an overlay without moving the
picture. F1 reveals hidden controls before opening shortcut help.

AdaptiveLayoutTests covers startup, width breakpoints, resize transitions at
multiple display scales, F11, F1 and fullscreen restoration using headless UI.