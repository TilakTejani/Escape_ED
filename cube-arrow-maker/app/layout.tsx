import type { Metadata } from 'next'
import './globals.css'

export const metadata: Metadata = {
  title: 'Cube Arrow — Level Maker',
  description: 'Design puzzle levels for the Cube Arrow game',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className="h-full">
      <body className="h-full overflow-hidden">{children}</body>
    </html>
  )
}
