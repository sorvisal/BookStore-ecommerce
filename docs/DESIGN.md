# BookNest Design System

## Brand
- **Name:** BookNest
- **Tagline:** Your cozy corner for great books
- **Style:** Elegant, warm, modern

## Typography
- **Headings:** Playfair Display (Google Fonts)
- **Body:** Inter (Google Fonts)

## Colors (CSS Variables)
- `--ink: #1b2236` - Primary text color
- `--accent: #e8a33d` - Primary accent color
- `--accent-d: #c98614` - Darker accent color
- `--bg: #faf7f2` - Background color
- `--muted: #6b7280` - Muted text color
- `--radius: 14px` - Border radius

## Bootstrap + Icons
- Bootstrap 5 (CDN)
- Bootstrap Icons (CDN)

## Components

### Cards
- Background: white
- Border radius: `--radius` (14px)
- Shadow: soft shadow
- Hover: lift with `transform: translateY(-6px)`

### Buttons
- `.btn-accent`: Primary button with `--accent` background and `--ink` text
- Hover: `--accent-d`

### Status Badges
- **Pending:** amber (#f59e0b)
- **Processing:** blue (#3b82f6)
- **Shipped:** indigo (#6366f1)
- **Delivered:** green (#22c55e)
- **Cancelled:** red (#ef4444)

## Images
- All book images must use `onerror="this.src='/images/placeholder.svg'"` as fallback
- When ImageUrl is empty, fall back to placeholder

## Responsive
- Mobile-first approach
- No inline styles where a CSS class fits
- Use Bootstrap grid and utilities
