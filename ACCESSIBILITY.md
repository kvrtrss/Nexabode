# Accessibility

Nexabode aims to become easier to use for people with different accessibility needs.

## Supported Environment

Nexabode is a Windows desktop application built with WPF. Accessibility behavior may depend on the Windows version, application controls, display settings, and assistive technologies being used.

## What has been done

- **Keyboard:** Ctrl+1 to Ctrl+9 and Ctrl+0 open the pages in the order of the side bar, Ctrl+F goes to the page's search box, and Ctrl+N does what the page's main button does. Tab follows the order of the controls on screen. The payment form is ordered the way it is used.
- **Names for screen readers:** the notification bell (including how many are new), the light and dark button, the calendar's previous and next buttons, and the fields of the payment, utility bill, sign-in and recovery forms have accessible names.
- **Contrast:** the colours were measured against the 4.5 to 1 guideline for normal text. Body text and the main grey text pass in the light and dark themes, and the status colours (paid, overdue, available and so on) were adjusted so they pass too, both on a plain surface and on their own soft coloured label. White text sits on a fixed deep indigo in both themes. Disabled buttons are deliberately dimmer, as the guidelines allow.
- **Small screens:** the side bar folds to icons on narrow windows, and the main window never opens larger than the screen's usable area.
- **Colour is not the only signal:** statuses are shown with words as well as colours.
- **Mouse and keyboard parity:** a right-click menu on tables offers the same actions as the buttons above them, and every action is also reachable by button.

## What has not been checked yet

- Use with a screen reader such as Narrator has not been tested.
- Focus order and a clearly visible focus outline have not been checked in every window and dialog.
- Behaviour at larger Windows text sizes and 150% to 200% display scaling has not been checked.
- The calendar's coloured markers on the Dashboard rely on colour, with a legend. A count or shape would be better.
- The side bar's own menu button comes from the WPF-UI control library and its accessible name has not been reviewed.

These are things still to evaluate, not claims that every feature is supported.

## Reporting Accessibility Barriers

If you encounter an accessibility issue, please open an issue in the Nexabode GitHub repository. Describe the affected screen or control, what you expected to happen, and the relevant Windows or assistive-technology setup.

Please avoid including personal or sensitive information in reports.

## Ongoing Improvements

Accessibility feedback is welcome. Improvements will be evaluated as the project develops.
