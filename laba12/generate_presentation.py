import sys
from pptx import Presentation
from pptx.util import Inches, Pt
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN
from pptx.enum.shapes import MSO_SHAPE

def create_deck(filename):
    prs = Presentation()
    prs.slide_width = Inches(13.333)
    prs.slide_height = Inches(7.5)
    blank_layout = prs.slide_layouts[6]

    # Color Palette: Minimalist Swiss Editorial (Pure, High Contrast, No Cheesy AI fluff)
    BG_COLOR = RGBColor(255, 255, 255)       # Pure White
    TEXT_MAIN = RGBColor(17, 24, 39)         # Jet Charcoal #111827
    TEXT_MUTED = RGBColor(107, 114, 128)     # Cool Gray #6B7280
    TEXT_ACCENT = RGBColor(30, 41, 59)       # Deep Slate #1E293B
    BORDER_COLOR = RGBColor(229, 231, 235)   # Subtle Hairline #E5E7EB
    CARD_BG = RGBColor(249, 250, 251)        # Very subtle tinted white #F9FAFB

    FONT_FAMILY = "Arial"

    def set_slide_background(slide):
        background = slide.background
        fill = background.fill
        fill.solid()
        fill.fore_color.rgb = BG_COLOR

    def add_header(slide, number_str, title_str, subtitle_str):
        # Category / Number tag
        tag_box = slide.shapes.add_textbox(Inches(1.0), Inches(0.65), Inches(11.333), Inches(0.35))
        tf_tag = tag_box.text_frame
        tf_tag.word_wrap = True
        tf_tag.margin_left = tf_tag.margin_top = tf_tag.margin_right = tf_tag.margin_bottom = 0
        p_tag = tf_tag.paragraphs[0]
        p_tag.text = number_str.upper()
        p_tag.font.name = FONT_FAMILY
        p_tag.font.size = Pt(11)
        p_tag.font.bold = True
        p_tag.font.color.rgb = TEXT_MUTED

        # Main Title
        title_box = slide.shapes.add_textbox(Inches(1.0), Inches(0.95), Inches(11.333), Inches(0.7))
        tf_title = title_box.text_frame
        tf_title.word_wrap = True
        tf_title.margin_left = tf_title.margin_top = tf_title.margin_right = tf_title.margin_bottom = 0
        p_title = tf_title.paragraphs[0]
        p_title.text = title_str
        p_title.font.name = FONT_FAMILY
        p_title.font.size = Pt(28)
        p_title.font.bold = True
        p_title.font.color.rgb = TEXT_MAIN

        # Subtitle
        sub_box = slide.shapes.add_textbox(Inches(1.0), Inches(1.68), Inches(11.333), Inches(0.45))
        tf_sub = sub_box.text_frame
        tf_sub.word_wrap = True
        tf_sub.margin_left = tf_sub.margin_top = tf_sub.margin_right = tf_sub.margin_bottom = 0
        p_sub = tf_sub.paragraphs[0]
        p_sub.text = subtitle_str
        p_sub.font.name = FONT_FAMILY
        p_sub.font.size = Pt(13)
        p_sub.font.color.rgb = TEXT_MUTED

        # Divider line
        line = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, Inches(1.0), Inches(2.22), Inches(11.333), Inches(0.015))
        line.fill.solid()
        line.fill.fore_color.rgb = BORDER_COLOR
        line.line.color.rgb = BORDER_COLOR

    # =========================================================================
    # SLIDE 1: TITLE SLIDE
    # =========================================================================
    s1 = prs.slides.add_slide(blank_layout)
    set_slide_background(s1)

    # Top indicator
    top_meta = s1.shapes.add_textbox(Inches(1.2), Inches(1.2), Inches(10.0), Inches(0.4))
    tf1 = top_meta.text_frame
    tf1.word_wrap = True
    p = tf1.paragraphs[0]
    p.text = "ПРЕЗЕНТАЦИЯ ПО ГЕОГРАФИИ"
    p.font.name = FONT_FAMILY
    p.font.size = Pt(11)
    p.font.bold = True
    p.font.color.rgb = TEXT_MUTED

    # Huge Main Title
    title_box = s1.shapes.add_textbox(Inches(1.2), Inches(1.8), Inches(10.5), Inches(1.4))
    tf_main = title_box.text_frame
    tf_main.word_wrap = True
    p = tf_main.paragraphs[0]
    p.text = "Климат США"
    p.font.name = FONT_FAMILY
    p.font.size = Pt(54)
    p.font.bold = True
    p.font.color.rgb = TEXT_MAIN

    # Subtitle
    sub_box = s1.shapes.add_textbox(Inches(1.2), Inches(3.3), Inches(10.0), Inches(0.8))
    tf_sub = sub_box.text_frame
    tf_sub.word_wrap = True
    p = tf_sub.paragraphs[0]
    p.text = "Природные факторы, климатические пояса и региональные контрасты"
    p.font.name = FONT_FAMILY
    p.font.size = Pt(18)
    p.font.color.rgb = TEXT_MUTED

    # Minimal separator rule
    line = s1.shapes.add_shape(MSO_SHAPE.RECTANGLE, Inches(1.2), Inches(4.3), Inches(10.9), Inches(0.015))
    line.fill.solid()
    line.fill.fore_color.rgb = BORDER_COLOR
    line.line.color.rgb = BORDER_COLOR

    # Author Metadata Block (prominently stating user requirement)
    auth_box = s1.shapes.add_textbox(Inches(1.2), Inches(4.8), Inches(5.0), Inches(1.6))
    tf_auth = auth_box.text_frame
    tf_auth.word_wrap = True

    p0 = tf_auth.paragraphs[0]
    p0.text = "ВЫПОЛНИЛ"
    p0.font.name = FONT_FAMILY
    p0.font.size = Pt(10)
    p0.font.bold = True
    p0.font.color.rgb = TEXT_MUTED

    p1 = tf_auth.add_paragraph()
    p1.text = "Лазаревич Арсений"
    p1.font.name = FONT_FAMILY
    p1.font.size = Pt(20)
    p1.font.bold = True
    p1.font.color.rgb = TEXT_MAIN
    p1.space_before = Pt(4)

    p2 = tf_auth.add_paragraph()
    p2.text = "Группа ПО2311"
    p2.font.name = FONT_FAMILY
    p2.font.size = Pt(14)
    p2.font.color.rgb = TEXT_MUTED
    p2.space_before = Pt(2)

    # Right-side key facts preview
    meta_right = s1.shapes.add_textbox(Inches(7.2), Inches(4.8), Inches(4.9), Inches(1.6))
    tf_mr = meta_right.text_frame
    tf_mr.word_wrap = True

    p_r0 = tf_mr.paragraphs[0]
    p_r0.text = "ОСНОВНОЙ ТЕЗИС"
    p_r0.font.name = FONT_FAMILY
    p_r0.font.size = Pt(10)
    p_r0.font.bold = True
    p_r0.font.color.rgb = TEXT_MUTED

    p_r1 = tf_mr.add_paragraph()
    p_r1.text = "Благодаря масштабу территории и уникальному рельефу в США представлены почти все климатические зоны Земли — от арктической тундры до тропических островов."
    p_r1.font.name = FONT_FAMILY
    p_r1.font.size = Pt(13)
    p_r1.font.color.rgb = TEXT_ACCENT
    p_r1.space_before = Pt(4)

    # =========================================================================
    # SLIDE 2: КЛЮЧЕВЫЕ ФАКТОРЫ
    # =========================================================================
    s2 = prs.slides.add_slide(blank_layout)
    set_slide_background(s2)
    add_header(s2, "02 / ОБЩИЕ ФАКТОРЫ", "Ключевые климатообразующие факторы", "Три главных географических условия, определяющих погоду на континенте")

    cols2 = [
        ("01", "Океаническое влияние",
         "Тихий и Атлантический океаны формируют противоположные режимы:\n\n"
         "• Холодное Калифорнийское течение стабилизирует западное побережье и снижает температуры.\n"
         "• Теплый Гольфстрим насыщает влагой юго-восток и усиливает тропические циклоны.\n"
         "• Разница температур океанов создает мощный градиент давления."),
        
        ("02", "Меридиональный рельеф",
         "Горные системы ориентированы с севера на юг, а не с запада на восток:\n\n"
         "• Кордильеры на западе блокируют тихоокеанские осадки во внутренние районы.\n"
         "• Аппалачи на востоке умеренно задерживают воздушные массы.\n"
         "• Центральные равнины образуют открытый коридор для сквозного движения арктических и тропических масс."),

        ("03", "Широтный диапазон",
         "Колоссальная протяженность территории с севера на юг:\n\n"
         "• От мыса Барроу на Аляске (71° с.ш.) до острова Гавайи (19° с.ш.).\n"
         "• Континентальная часть (Lower 48) расположена между 25° и 49° с.ш.\n"
         "• Охватывает умеренный, субтропический, арктический и тропический пояса.")
    ]

    col_w = Inches(3.55)
    gap = Inches(0.34)
    left_start = Inches(1.0)
    top_pos = Inches(2.55)

    for i, (idx, head, body) in enumerate(cols2):
        cur_left = left_start + i * (col_w + gap)
        
        # Border box
        box = s2.shapes.add_shape(MSO_SHAPE.RECTANGLE, cur_left, top_pos, col_w, Inches(4.3))
        box.fill.solid()
        box.fill.fore_color.rgb = CARD_BG
        box.line.color.rgb = BORDER_COLOR
        box.line.width = Pt(1)

        tb = s2.shapes.add_textbox(cur_left + Inches(0.3), top_pos + Inches(0.3), col_w - Inches(0.6), Inches(3.7))
        tf = tb.text_frame
        tf.word_wrap = True
        tf.margin_left = tf.margin_top = tf.margin_right = tf.margin_bottom = 0

        p0 = tf.paragraphs[0]
        p0.text = idx
        p0.font.name = FONT_FAMILY
        p0.font.size = Pt(11)
        p0.font.bold = True
        p0.font.color.rgb = TEXT_MUTED

        p1 = tf.add_paragraph()
        p1.text = head
        p1.font.name = FONT_FAMILY
        p1.font.size = Pt(16)
        p1.font.bold = True
        p1.font.color.rgb = TEXT_MAIN
        p1.space_before = Pt(6)

        p2 = tf.add_paragraph()
        p2.text = body
        p2.font.name = FONT_FAMILY
        p2.font.size = Pt(11)
        p2.font.color.rgb = TEXT_ACCENT
        p2.space_before = Pt(12)

    # =========================================================================
    # SLIDE 3: ТИХООКЕАНСКИЙ РЕГИОН И ЗАПАД
    # =========================================================================
    s3 = prs.slides.add_slide(blank_layout)
    set_slide_background(s3)
    add_header(s3, "03 / ЗАПАДНОЕ ПОБЕРЕЖЬЕ", "Тихоокеанский регион и Кордильеры", "От влажных хвойных дождевых лесов до сухого средиземноморского юга")

    cols3 = [
        ("СЕВЕРО-ЗАПАД (ТИХООКЕАНСКИЙ)", "Штаты Вашингтон и Орегон", [
            ("Климатический тип", "Умеренный морской климат с океаническим влиянием."),
            ("Температурный режим", "Мягкая зима (+3...+7°C) без затяжных морозов, умеренно прохладное лето (+19...+23°C)."),
            ("Осадки", "Рекордный уровень осадков: до 2000–3500 мм в предгорьях Каскадных гор. Густые умеренные дождевые леса."),
            ("Характерная погода", "Постоянная высокая облачность, частые моросящие дожди и густые океанические туманы с октября по май.")
        ]),
        ("КАЛИФОРНИЙСКОЕ ПОБЕРЕЖЬЕ", "Центральная и Южная Калифорния", [
            ("Климатический тип", "Классический средиземноморский климат."),
            ("Температурный режим", "Теплая сухая погода летом (+22...+28°C), мягкая дождливая зима (+12...+16°C)."),
            ("Прибрежные туманы", "Холодное Калифорнийское течение порождает регулярный прибрежный туман (Marine Layer), охлаждающий Сан-Франциско."),
            ("Сезонные риски", "Длительные периоды засухи с мая по октябрь, сухие ветры «Санта-Ана» и высокий риск лесных пожаров.")
        ])
    ]

    col_w2 = Inches(5.5)
    gap2 = Inches(0.33)
    for i, (tag, title, items) in enumerate(cols3):
        cur_left = Inches(1.0) + i * (col_w2 + gap2)
        box = s3.shapes.add_shape(MSO_SHAPE.RECTANGLE, cur_left, top_pos, col_w2, Inches(4.3))
        box.fill.solid()
        box.fill.fore_color.rgb = CARD_BG
        box.line.color.rgb = BORDER_COLOR
        box.line.width = Pt(1)

        tb = s3.shapes.add_textbox(cur_left + Inches(0.35), top_pos + Inches(0.3), col_w2 - Inches(0.7), Inches(3.7))
        tf = tb.text_frame
        tf.word_wrap = True
        tf.margin_left = tf.margin_top = tf.margin_right = tf.margin_bottom = 0

        p0 = tf.paragraphs[0]
        p0.text = tag
        p0.font.name = FONT_FAMILY
        p0.font.size = Pt(10)
        p0.font.bold = True
        p0.font.color.rgb = TEXT_MUTED

        p1 = tf.add_paragraph()
        p1.text = title
        p1.font.name = FONT_FAMILY
        p1.font.size = Pt(18)
        p1.font.bold = True
        p1.font.color.rgb = TEXT_MAIN
        p1.space_before = Pt(4)

        for label, val in items:
            p_lbl = tf.add_paragraph()
            p_lbl.text = f"• {label}: "
            p_lbl.font.name = FONT_FAMILY
            p_lbl.font.size = Pt(11)
            p_lbl.font.bold = True
            p_lbl.font.color.rgb = TEXT_MAIN
            p_lbl.space_before = Pt(10)

            run = p_lbl.add_run()
            run.text = val
            run.font.name = FONT_FAMILY
            run.font.size = Pt(11)
            run.font.bold = False
            run.font.color.rgb = TEXT_ACCENT

    # =========================================================================
    # SLIDE 4: АРИДНЫЙ ЮГО-ЗАПАД И ПУСТЫНИ
    # =========================================================================
    s4 = prs.slides.add_slide(blank_layout)
    set_slide_background(s4)
    add_header(s4, "04 / ЮГО-ЗАПАД", "Аридный Юго-Запад и Пустыни", "Эффект дождевой тени Кордильер и экстремальные термические максимумы")

    # Left Column: Structure / Geography
    left_w = Inches(6.8)
    cur_left = Inches(1.0)

    box_l = s4.shapes.add_shape(MSO_SHAPE.RECTANGLE, cur_left, top_pos, left_w, Inches(4.3))
    box_l.fill.solid()
    box_l.fill.fore_color.rgb = CARD_BG
    box_l.line.color.rgb = BORDER_COLOR
    box_l.line.width = Pt(1)

    tb_l = s4.shapes.add_textbox(cur_left + Inches(0.35), top_pos + Inches(0.3), left_w - Inches(0.7), Inches(3.7))
    tf_l = tb_l.text_frame
    tf_l.word_wrap = True
    tf_l.margin_left = tf_l.margin_top = tf_l.margin_right = tf_l.margin_bottom = 0

    p = tf_l.paragraphs[0]
    p.text = "ГЕОГРАФИЯ И МЕХАНИЗМ АРИДНОСТИ"
    p.font.name = FONT_FAMILY
    p.font.size = Pt(10)
    p.font.bold = True
    p.font.color.rgb = TEXT_MUTED

    p = tf_l.add_paragraph()
    p.text = "Штаты Невада, Аризона, Нью-Мексико, Юта"
    p.font.name = FONT_FAMILY
    p.font.size = Pt(18)
    p.font.bold = True
    p.font.color.rgb = TEXT_MAIN
    p.space_before = Pt(4)

    sw_items = [
        ("Эффект «дождевой тени»", "Хребты Сьерра-Невада и Каскадные горы полностью задерживают тихоокеанский влажный воздух, оставляя межгорные плато без осадков."),
        ("Норма осадков", "От 70 до 250 мм в год. Во многих районах дожди не выпадают месяцами."),
        ("Летний муссон", "В июле-августе на юго-запад проникает влажный воздух с Калифорнийского залива, вызывая кратковременные ливни и внезапные паводки (flash floods)."),
        ("Суточные амплитуды", "Из-за сухой атмосферы и отсутствия облаков тепло быстро уходит: ночью температура падает на 20–25°C относительно дневного пика.")
    ]
    for lbl, val in sw_items:
        p_item = tf_l.add_paragraph()
        p_item.text = f"• {lbl}: "
        p_item.font.name = FONT_FAMILY
        p_item.font.size = Pt(11)
        p_item.font.bold = True
        p_item.font.color.rgb = TEXT_MAIN
        p_item.space_before = Pt(8)

        run = p_item.add_run()
        run.text = val
        run.font.name = FONT_FAMILY
        run.font.size = Pt(11)
        run.font.bold = False
        run.font.color.rgb = TEXT_ACCENT

    # Right Column: Big Metrics / Death Valley
    right_w = Inches(4.2)
    cur_right = Inches(8.13)

    metrics = [
        ("+56.7 °C", "Мировой рекорд жары", "Зафиксирован в Долине Смерти (Калифорния, Фернис-Крик). Абсолютный максимум температуры воздуха на Земле."),
        ("< 50 мм", "Годовые минимумы", "В самых засушливых котловинах пустыни Мохаве осадки практически отсутствуют годами."),
        ("+45 °C", "Стандартная норма лета", "Среднедневная летняя температура в городах Феникс и Лас-Вегас.")
    ]

    m_h = Inches(1.33)
    m_gap = Inches(0.15)
    for j, (num, m_title, m_desc) in enumerate(metrics):
        m_top = top_pos + j * (m_h + m_gap)
        m_box = s4.shapes.add_shape(MSO_SHAPE.RECTANGLE, cur_right, m_top, right_w, m_h)
        m_box.fill.solid()
        m_box.fill.fore_color.rgb = CARD_BG
        m_box.line.color.rgb = BORDER_COLOR
        m_box.line.width = Pt(1)

        m_tb = s4.shapes.add_textbox(cur_right + Inches(0.25), m_top + Inches(0.15), right_w - Inches(0.5), m_h - Inches(0.3))
        m_tf = m_tb.text_frame
        m_tf.word_wrap = True
        m_tf.margin_left = m_tf.margin_top = m_tf.margin_right = m_tf.margin_bottom = 0

        p_m0 = m_tf.paragraphs[0]
        p_m0.text = num
        p_m0.font.name = FONT_FAMILY
        p_m0.font.size = Pt(20)
        p_m0.font.bold = True
        p_m0.font.color.rgb = TEXT_MAIN

        p_m1 = m_tf.add_paragraph()
        p_m1.text = m_title
        p_m1.font.name = FONT_FAMILY
        p_m1.font.size = Pt(10)
        p_m1.font.bold = True
        p_m1.font.color.rgb = TEXT_MUTED

        p_m2 = m_tf.add_paragraph()
        p_m2.text = m_desc
        p_m2.font.name = FONT_FAMILY
        p_m2.font.size = Pt(9.5)
        p_m2.font.color.rgb = TEXT_ACCENT

    # =========================================================================
    # SLIDE 5: ВЕЛИКИЕ РАВНИНЫ И АЛЛЕЯ ТОРНАДО
    # =========================================================================
    s5 = prs.slides.add_slide(blank_layout)
    set_slide_background(s5)
    add_header(s5, "05 / ВЕЛИКИЕ РАВНИНЫ И МИДВЕСТ", "Великие равнины и Аллея Торнадо", "Резкая континентальность и глобальный эпицентр опасных погодных явлений")

    cols5 = [
        ("КОНТИНЕНТАЛЬНЫЙ РЕЖИМ", "Внутренние равнины континента", [
            ("Географический охват", "Северная и Южная Дакота, Небраска, Канзас, Миннесота, Айова."),
            ("Экстремальные сезоны", "Зимы суровые и ветреные (до -25...-35°C), лето жаркое и душное (до +35...+40°C)."),
            ("Отсутствие барьеров", "Прямое столкновение ледяного полярного воздуха из Канады и горячего субтропического из Техаса."),
            ("Резкие смены погоды", "Температура может упасть или вырасти на 20–25°C всего за несколько часов при прохождении холодного фронта (Cold Wave).")
        ]),
        ("ФЕНОМЕН «АЛЛЕИ ТОРНАДО»", "Tornado Alley (Техас — Небраска)", [
            ("Механизм возникновения", "Сухой воздух со Скалистых гор натекает на влажные теплые массы с Мексиканского залива, создавая нестабильность атмосферы."),
            ("Суперъячейки", "Формирование мощных грозовых суперъячеек со шквалами, гигантским градом и смерчами."),
            ("Мировая концентрация", "В США регистрируется свыше 1200 торнадо ежегодно — больше, чем в любой другой стране планеты."),
            ("Пиковый сезон", "С апреля по июнь на Великих равнинах наблюдается максимальная частота разрушительных вихрей.")
        ])
    ]

    for i, (tag, title, items) in enumerate(cols5):
        cur_left = Inches(1.0) + i * (col_w2 + gap2)
        box = s5.shapes.add_shape(MSO_SHAPE.RECTANGLE, cur_left, top_pos, col_w2, Inches(4.3))
        box.fill.solid()
        box.fill.fore_color.rgb = CARD_BG
        box.line.color.rgb = BORDER_COLOR
        box.line.width = Pt(1)

        tb = s5.shapes.add_textbox(cur_left + Inches(0.35), top_pos + Inches(0.3), col_w2 - Inches(0.7), Inches(3.7))
        tf = tb.text_frame
        tf.word_wrap = True
        tf.margin_left = tf.margin_top = tf.margin_right = tf.margin_bottom = 0

        p0 = tf.paragraphs[0]
        p0.text = tag
        p0.font.name = FONT_FAMILY
        p0.font.size = Pt(10)
        p0.font.bold = True
        p0.font.color.rgb = TEXT_MUTED

        p1 = tf.add_paragraph()
        p1.text = title
        p1.font.name = FONT_FAMILY
        p1.font.size = Pt(18)
        p1.font.bold = True
        p1.font.color.rgb = TEXT_MAIN
        p1.space_before = Pt(4)

        for label, val in items:
            p_lbl = tf.add_paragraph()
            p_lbl.text = f"• {label}: "
            p_lbl.font.name = FONT_FAMILY
            p_lbl.font.size = Pt(11)
            p_lbl.font.bold = True
            p_lbl.font.color.rgb = TEXT_MAIN
            p_lbl.space_before = Pt(10)

            run = p_lbl.add_run()
            run.text = val
            run.font.name = FONT_FAMILY
            run.font.size = Pt(11)
            run.font.bold = False
            run.font.color.rgb = TEXT_ACCENT

    # =========================================================================
    # SLIDE 6: ВОСТОК И ЮГО-ВОСТОК
    # =========================================================================
    s6 = prs.slides.add_slide(blank_layout)
    set_slide_background(s6)
    add_header(s6, "06 / ВОСТОЧНОЕ ПОБЕРЕЖЬЕ", "Северо-Восток и Субтропический Юг", "Контраст влажного умеренного севера и зоны атлантических ураганов")

    cols6 = [
        ("СЕВЕРО-ВОСТОК И ВЕЛИКИЕ ОЗЕРА", "Нью-Йорк, Бостон, Чикаго", [
            ("Климатический тип", "Влажный континентальный климат с выраженными 4 сезонами."),
            ("Зимний период", "Снежные заносы, метели и штормы «норд-осты» (Nor'easters) с Атлантики. Температура -5...-15°C."),
            ("Эффект Великих озер", "Lake-effect snow: холодный воздух над теплой водой озер вызывает колоссальные снегопады в Баффало и Сиракузах."),
            ("Лето", "Теплое и влажное (+24...+29°C), периодические грозы.")
        ]),
        ("ЮГО-ВОСТОК И ФЛОРИДА", "Флорида, Джорджия, Луизиана, Техас", [
            ("Климатический тип", "Влажный субтропический, на самом юге Флориды (Майами) — тропический влажный."),
            ("Летний период", "Долгое жаркое лето (+30...+34°C) с практически ежедневными послеполуденными грозами."),
            ("Сезон ураганов", "С 1 июня по 30 ноября побережье подвергается ударам мощных тропических циклонов категории 3–5."),
            ("Осадки", "Равномерные обильные осадки круглый год (1200–1600 мм), отсутствие заморозков на юге.")
        ])
    ]

    for i, (tag, title, items) in enumerate(cols6):
        cur_left = Inches(1.0) + i * (col_w2 + gap2)
        box = s6.shapes.add_shape(MSO_SHAPE.RECTANGLE, cur_left, top_pos, col_w2, Inches(4.3))
        box.fill.solid()
        box.fill.fore_color.rgb = CARD_BG
        box.line.color.rgb = BORDER_COLOR
        box.line.width = Pt(1)

        tb = s6.shapes.add_textbox(cur_left + Inches(0.35), top_pos + Inches(0.3), col_w2 - Inches(0.7), Inches(3.7))
        tf = tb.text_frame
        tf.word_wrap = True
        tf.margin_left = tf.margin_top = tf.margin_right = tf.margin_bottom = 0

        p0 = tf.paragraphs[0]
        p0.text = tag
        p0.font.name = FONT_FAMILY
        p0.font.size = Pt(10)
        p0.font.bold = True
        p0.font.color.rgb = TEXT_MUTED

        p1 = tf.add_paragraph()
        p1.text = title
        p1.font.name = FONT_FAMILY
        p1.font.size = Pt(18)
        p1.font.bold = True
        p1.font.color.rgb = TEXT_MAIN
        p1.space_before = Pt(4)

        for label, val in items:
            p_lbl = tf.add_paragraph()
            p_lbl.text = f"• {label}: "
            p_lbl.font.name = FONT_FAMILY
            p_lbl.font.size = Pt(11)
            p_lbl.font.bold = True
            p_lbl.font.color.rgb = TEXT_MAIN
            p_lbl.space_before = Pt(10)

            run = p_lbl.add_run()
            run.text = val
            run.font.name = FONT_FAMILY
            run.font.size = Pt(11)
            run.font.bold = False
            run.font.color.rgb = TEXT_ACCENT

    # =========================================================================
    # SLIDE 7: ЭКСТРЕМАЛЬНЫЕ ЗОНЫ И ВЫВОДЫ
    # =========================================================================
    s7 = prs.slides.add_slide(blank_layout)
    set_slide_background(s7)
    add_header(s7, "07 / ЭКСТРЕМАЛЬНЫЕ РЕГИОНЫ И ВЫВОДЫ", "Аляска, Гавайи и Общий итог", "Контрасты внешних территорий и уникальность климатической карты США")

    cols7 = [
        ("СУБАРКТИКА И АРКТИКА", "Аляска",
         "• Арктический и субарктический климатические пояса.\n"
         "• Зимние минимумы достигают -50...-62°C во внутренних районах (Юкон).\n"
         "• Вечная мерзлота покрывает около 85% территории штата.\n"
         "• На южном побережье (Джуно) климат мягче благодаря Тихому океану, но выпадает до 4000 мм осадков."),

        ("ТРОПИЧЕСКАЯ ОКЕАНИЯ", "Гавайские острова",
         "• Морской тропический пассатный климат.\n"
         "• Круглогодично стабильная комфортная температура: +24...+30°C.\n"
         "• Рельеф создает контрастные микроклиматы: наветренные склоны крайне влажные, подветренные — засушливые.\n"
         "• Гора Ваиалеале — одно из самых дождливых мест мира (до 11 500 мм осадков)."),

        ("ГЛАВНЫЙ ИТОГ", "Заключение",
         "• Полный спектр: В пределах одной страны представлены почти все климатические зоны мира (от арктических льдов до тропических атоллов).\n"
         "• Контрастность: Отсутствие поперечных горных барьеров делает погоду США одной из самых динамичных и штормовых на планете.\n"
         "• Экономическое влияние: Разнообразие климата сформировало специализированные пояса сельского хозяйства (Кукурузный, Пшеничный, Хлопковый).")
    ]

    for i, (tag, title, body) in enumerate(cols7):
        cur_left = left_start + i * (col_w + gap)
        box = s7.shapes.add_shape(MSO_SHAPE.RECTANGLE, cur_left, top_pos, col_w, Inches(4.3))
        box.fill.solid()
        box.fill.fore_color.rgb = CARD_BG
        box.line.color.rgb = BORDER_COLOR
        box.line.width = Pt(1)

        tb = s7.shapes.add_textbox(cur_left + Inches(0.3), top_pos + Inches(0.3), col_w - Inches(0.6), Inches(3.7))
        tf = tb.text_frame
        tf.word_wrap = True
        tf.margin_left = tf.margin_top = tf.margin_right = tf.margin_bottom = 0

        p0 = tf.paragraphs[0]
        p0.text = tag
        p0.font.name = FONT_FAMILY
        p0.font.size = Pt(10)
        p0.font.bold = True
        p0.font.color.rgb = TEXT_MUTED

        p1 = tf.add_paragraph()
        p1.text = title
        p1.font.name = FONT_FAMILY
        p1.font.size = Pt(16)
        p1.font.bold = True
        p1.font.color.rgb = TEXT_MAIN
        p1.space_before = Pt(4)

        p2 = tf.add_paragraph()
        p2.text = body
        p2.font.name = FONT_FAMILY
        p2.font.size = Pt(10.5)
        p2.font.color.rgb = TEXT_ACCENT
        p2.space_before = Pt(10)

    prs.save(filename)
    print(f"Presentation saved successfully to: {filename}")

if __name__ == "__main__":
    out_file = sys.argv[1] if len(sys.argv) > 1 else "climate_usa.pptx"
    create_deck(out_file)
